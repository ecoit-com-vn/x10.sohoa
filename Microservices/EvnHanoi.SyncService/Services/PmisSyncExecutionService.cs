using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Internal;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using Serilog;

namespace EvnHanoi.SyncService.Services;

public class PmisSyncExecutionService : IPmisSyncExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // PMIS đôi khi trả field maTBA/maDuongDay là CHUỖI RỖNG thay vì bỏ hẳn field (null) — "??" thường
    // không bắt được trường hợp này (chuỗi rỗng không phải null nên thắng luôn, fallback không bao giờ
    // chạy tới). Coi cả 2 là "thiếu dữ liệu" như nhau trước khi áp dụng fallback, tránh tái diễn đúng
    // bug orphan (ParentPmisCode="" vẫn bị EquipmentRepository coi là "hợp lệ, không có cha" và ghi
    // INFRASTRUCTURE_ID=NULL — xem pmis_sync_equipment_orphan_null_infra_id).
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    // An toàn: tối đa tài liệu/đối tượng/lần đồng bộ — tính theo TỔNG SỐ BẢN GHI, không phải số TRANG, vì
    // PageSize giờ admin tự cấu hình được (xem lý do tương tự ở PmisScheduledSyncJob.MaxTotalRecords).
    // Dùng chung đúng 1 nguồn (PmisPaging.MaxTotalRecordsPerRun) để không lệch với hằng số bên đó.
    private const int DocumentMaxTotalRecords = PmisPaging.MaxTotalRecordsPerRun;
    private const int DocumentUpsertBatchSize = 20; // gửi theo lô, tránh 1 request base64 hoá hết cả nghìn tài liệu

    // An toàn: tối đa số LẦN GỌI SyncDocumentsForOwnerAsync (1 lần/Trạm/Đường dây/Thiết bị vừa lưu thành
    // công) trong 1 lượt đồng bộ — mỗi lần lại tự phân trang + tải file PMIS TUẦN TỰ (xem
    // SyncDocumentsForOwnerAsync), không có trần trước đây có thể khiến 1 lượt có hàng chục nghìn bản ghi
    // thành công chạy RẤT lâu chỉ để đồng bộ tài liệu đính kèm. Owner vượt trần bị BỎ QUA hẳn tài liệu ở
    // lượt này (không gọi PMIS) — PmisScheduledSyncJob coi việc chạm trần này TƯƠNG ĐƯƠNG với chạm giới hạn
    // an toàn chính (HasHitSafetyCap), dừng hẳn phân trang tại đó và lưu lại vị trí vào SyncConfig.SyncCursor
    // để lượt sau tiếp tục đúng từ owner đó — không ower nào bị bỏ sót vĩnh viễn.
    private const int MaxDocumentSyncCallsPerRun = 2000;
    private int _documentSyncCallsThisRun;

    /// <summary>true nếu lượt này đã dừng đồng bộ tài liệu đính kèm vì chạm <see cref="MaxDocumentSyncCallsPerRun"/>
    /// — PmisScheduledSyncJob đọc cờ này sau mỗi trang để quyết định có nên dừng hẳn phân trang (giống
    /// HasHitSafetyCap) hay không, dù bản thân trang đó vẫn còn dữ liệu.</summary>
    public bool DocumentSyncBudgetExhausted => _documentSyncCallsThisRun >= MaxDocumentSyncCallsPerRun;

    private bool TryConsumeDocumentSyncCallBudget()
    {
        if (_documentSyncCallsThisRun >= MaxDocumentSyncCallsPerRun) return false;
        _documentSyncCallsThisRun++;
        if (_documentSyncCallsThisRun == MaxDocumentSyncCallsPerRun)
        {
            Log.Warning("PmisSyncExecutionService: đồng bộ tài liệu đính kèm đạt trần {Max} owner/lượt này — dừng lại, sẽ tự tiếp tục ở lượt sau (xem SyncConfig.SyncCursor).", MaxDocumentSyncCallsPerRun);
        }
        return true;
    }

    // An toàn: giới hạn số lượt gọi PMIS THẬT (ChiTietThietBi + tải ảnh QR) trong 1 lượt đồng bộ Thiết bị —
    // mỗi lượt là 1 round-trip PMIS TUẦN TỰ (xem SyncEquipmentAsync), không giới hạn trước đây có thể khiến
    // 1 lượt xử lý hàng nghìn thiết bị TBA chạy quá lâu và bị SyncHistoryWatchdogJob đánh rớt vì treo
    // RUNNING quá lâu. Thiết bị vượt trần vẫn được lưu đầy đủ Code/Name/EquipmentTypeCode... chỉ thiếu
    // ThongSoKyThuat/QR mới của lượt này. LƯU Ý: ngân sách này dùng CHUNG cho CẢ LƯỢT RunEquipmentAsync
    // (tất cả Trạm/Đường dây cha), KHÔNG reset theo từng cha — nếu danh sách cha luôn duyệt cùng 1 thứ tự
    // mỗi lượt (đã đúng như vậy trước khi sửa), các cha ở cuối danh sách sẽ KHÔNG BAO GIỜ nhận được ngân
    // sách, trái với PMIS trả TOÀN BỘ dữ liệu mỗi lượt tưởng như "tự enrich lại ở lượt sau" — không hề tự
    // enrich vì luôn hết ngân sách ở đúng nhóm cha đầu danh sách. Đã sửa bằng cơ chế XOAY VÒNG (rotate)
    // danh sách cha theo SyncConfig.SyncCursor (xem PmisScheduledSyncJob.RunEquipmentAsync +
    // EquipmentDetailBudgetExhausted bên dưới) — mỗi lượt ưu tiên ngân sách cho nhóm cha KHÁC nhau, đảm bảo
    // mọi thiết bị cuối cùng đều được enrich qua nhiều lượt chạy.
    private const int MaxEquipmentDetailCallsPerRun = 500;
    private int _equipmentDetailCallsThisRun;

    /// <summary>true nếu lượt này đã dùng hết ngân sách gọi PMIS thật cho Thiết bị (ChiTietThietBi/QR) —
    /// PmisScheduledSyncJob dùng để biết TỪ CHA NÀO trở đi trong lượt này không còn được enrich, làm điểm
    /// bắt đầu xoay vòng ưu tiên cho lượt kế tiếp (xem SyncConfig.SyncCursor).</summary>
    public bool EquipmentDetailBudgetExhausted => _equipmentDetailCallsThisRun >= MaxEquipmentDetailCallsPerRun;

    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;
    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;

    // Danh mục loại thiết bị PMIS (maLoaiTB -> tenLoaiTB) — tải 1 lần/vòng đời service (Scoped: 1 lần
    // đồng bộ tự động, hoặc 1 lần lưu thủ công), KHÔNG tải lại theo từng trang/từng thiết bị. Đây là
    // NGUỒN TÊN LOẠI THIẾT BỊ CHUẨN của chính PMIS (API 3/5 "DanhSachLoaiThietBi(DuongDay)") — đáng tin
    // hơn tenLoaiTB đính kèm từng dòng thiết bị (có thể null/thiếu tuỳ dữ liệu PMIS), dùng để đặt tên khi
    // hệ thống tự tạo EquipmentTypes mới cho 1 loại thiết bị lần đầu gặp (xem ResolveOrCreateEquipmentTypeIdAsync).
    private Dictionary<string, string>? _substationDeviceTypeNames;
    private Dictionary<string, string>? _lineDeviceTypeNames;

    public PmisSyncExecutionService(
        IEquipmentServiceClient equipmentServiceClient, ISyncHistoryRepository syncHistoryRepository, IPmisClient pmisClient,
        IPmisEndpointConfigProvider endpointConfigProvider)
    {
        _equipmentServiceClient = equipmentServiceClient;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
        _endpointConfigProvider = endpointConfigProvider;
    }

    /// <summary>Số bản ghi/trang admin đã cấu hình cho apiCode này qua "Cấu hình kết nối API" — mặc định
    /// <see cref="PmisPaging.DefaultPageSize"/> nếu API chưa cấu hình/chưa bật.</summary>
    private async Task<int> GetPageSizeAsync(string apiCode) =>
        (await _endpointConfigProvider.GetEndpointAsync(apiCode))?.PageSize ?? PmisPaging.DefaultPageSize;

    /// <summary>Tra tên loại thiết bị chuẩn từ danh mục PMIS (API 3/5) — null nếu tra lỗi (PMIS tạm gián
    /// đoạn) hoặc không tìm thấy mã, để caller tự fallback sang tenLoaiTB đính kèm dòng thiết bị.</summary>
    private async Task<string?> ResolveDeviceTypeNameAsync(bool isSubstationDevice, string? maLoaiTB)
    {
        if (string.IsNullOrWhiteSpace(maLoaiTB)) return null;

        var dict = isSubstationDevice
            ? _substationDeviceTypeNames ??= await LoadDeviceTypeNamesAsync(isSubstationDevice: true)
            : _lineDeviceTypeNames ??= await LoadDeviceTypeNamesAsync(isSubstationDevice: false);

        return dict.TryGetValue(maLoaiTB, out var name) ? name : null;
    }

    /// <summary>Luôn trả về Dictionary (rỗng nếu PMIS lỗi) — KHÔNG để null lọt qua caller's `??=`, vì null
    /// sẽ khiến MỌI thiết bị tiếp theo trong cùng lượt chạy này thử tải lại danh mục 1 lần nữa (PMIS đang
    /// lỗi thì lặp lại vô ích hàng trăm/nghìn lần cho từng thiết bị) — rỗng thì chỉ thử đúng 1 lần/lượt.</summary>
    private async Task<Dictionary<string, string>> LoadDeviceTypeNamesAsync(bool isSubstationDevice)
    {
        try
        {
            var apiCode = isSubstationDevice ? "SUBSTATION_DEVICE_TYPE_LIST" : "LINE_DEVICE_TYPE_LIST";
            var request = new PmisDeviceTypeSearchRequest { Take = await GetPageSizeAsync(apiCode) };
            var items = isSubstationDevice
                ? (await _pmisClient.GetSubstationDeviceTypesAsync(request)).Items
                : (await _pmisClient.GetLineDeviceTypesAsync(request)).Items;

            return items
                .Where(x => !string.IsNullOrWhiteSpace(x.MaLoaiTB))
                .GroupBy(x => x.MaLoaiTB, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().TenLoaiTB, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi khi tải danh mục loại thiết bị PMIS ({Kind}), dùng tạm tenLoaiTB đính kèm dòng thiết bị cho cả lượt chạy này.",
                isSubstationDevice ? "TBA" : "đường dây");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Gửi 1 lô upsert Trạm/Đường dây — bắt lỗi RIÊNG cho lô này thay vì để văng ra ngoài, vì
    /// SyncInfrastructureAsync (đường dây) giờ gửi TỐI ĐA 2 lô/trang (trục, rồi nhánh — xem 2-pass bên
    /// dưới): nếu để lô nhánh lỗi (mất kết nối tạm thời) làm cả method throw, lô trục ĐÃ LƯU THÀNH CÔNG
    /// trước đó sẽ bị caller (PmisManualSyncController/PmisScheduledSyncJob) coi nhầm là CẢ LƯỢT thất bại
    /// (100% failed) dù trục thật sự đã lưu — mất dấu thành công thật. Trả về kết quả Failed cho ĐÚNG các
    /// bản ghi trong lô lỗi, không ảnh hưởng lô còn lại.</summary>
    private async Task<List<UpsertInfrastructureFromPmisResult>> UpsertInfrastructureSafeAsync(List<UpsertInfrastructureFromPmisRequest> requests)
    {
        try
        {
            return await _equipmentServiceClient.UpsertInfrastructureAsync(requests);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi gửi 1 lô Trạm/Đường dây ({Count} bản ghi) sang EquipmentService — đánh dấu cả lô này thất bại, không ảnh hưởng các lô khác trong cùng trang.", requests.Count);
            return requests.Select(r => new UpsertInfrastructureFromPmisResult
            {
                PmisCode = r.PmisCode,
                Success = false,
                ErrorMessage = $"Lỗi gửi lô: {ex.Message}"
            }).ToList();
        }
    }

    /// <summary>Cùng lý do với UpsertInfrastructureSafeAsync — trước đây SyncEquipmentAsync gọi thẳng
    /// _equipmentServiceClient.UpsertEquipmentAsync không qua wrapper nào: nếu lỗi (mất kết nối, 401 do
    /// Internal:Token sai cấu hình...), exception văng ra TRƯỚC khi ghi bất kỳ SyncHistoryDetail nào cho lô
    /// này — khác Infrastructure (luôn có dòng Failed/item để tra), khiến "Lịch sử chi tiết" trống trơn cho
    /// cả lô, giảm khả năng truy vết dù lỗi vẫn được cô lập đúng ở tầng trang/lượt (PushPageAsync/Save).</summary>
    private async Task<List<UpsertEquipmentFromPmisResult>> UpsertEquipmentSafeAsync(List<UpsertEquipmentFromPmisRequest> requests)
    {
        try
        {
            return await _equipmentServiceClient.UpsertEquipmentAsync(requests);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi gửi 1 lô Thiết bị ({Count} bản ghi) sang EquipmentService — đánh dấu cả lô này thất bại, không ảnh hưởng các lô khác trong cùng trang.", requests.Count);
            return requests.Select(r => new UpsertEquipmentFromPmisResult
            {
                PmisCode = r.PmisCode,
                Success = false,
                ErrorMessage = $"Lỗi gửi lô: {ex.Message}"
            }).ToList();
        }
    }

    /// <summary>Trả về mã PMIS của Đường dây CHA thật sự — null nếu đây là đường trục gốc. XÁC NHẬN BẰNG
    /// DỮ LIỆU THẬT (gọi trực tiếp gateway PMIS 2026-09-23): field "maCha" của PMIS KHÔNG rỗng cho đường
    /// trục gốc như tài liệu ngầm định — nó bằng ĐÚNG "maDonVi" (mã đơn vị) của chính dòng đó (vd 214/233
    /// đường dây đơn vị HN02 có maCha="HN0200" trùng maDonVi="HN0200"), trong khi NHÁNH thật có maCha là mã
    /// 1 Đường dây KHÁC (khác maDonVi). Nếu không chặn, InfrastructureRepository sẽ tự SELECT
    /// INFRASTRUCTURE.PMIS_CODE = "HN0200" cho MỌI đường trục gốc — không bao giờ khớp (mã đơn vị không
    /// phải PMIS_CODE của Trạm/Đường dây nào) — khiến toàn bộ trục gốc bị coi nhầm là "chưa xác định được
    /// cha" (ParentUnresolved=true), cảnh báo giả tràn lan mỗi lượt đồng bộ.</summary>
    private static string? ResolveParentLinePmisCode(PmisLineDto item)
    {
        var maCha = BlankToNull(item.MaCha)?.Trim();
        if (maCha == null) return null;
        var maDonVi = BlankToNull(item.MaDonVi)?.Trim();
        return string.Equals(maCha, maDonVi, StringComparison.OrdinalIgnoreCase) ? null : maCha;
    }

    /// <summary>ParentPmisCode = ResolveParentLinePmisCode(item) — chỉ CHUYỂN TIẾP mã PMIS thô của đường
    /// trục cha, KHÔNG tự tra Id/mượn GridTypeId ở đây nữa (khác BuildLineUpsertRequest cũ, vốn phải tự dò
    /// theo tên qua _lineNameIndex trước khi PMIS có field "maCha" thật, 2026-09-23) — EquipmentService tự
    /// SELECT theo PMIS_CODE khi lưu (giống hệt ParentPmisCode của Thiết bị), kể cả việc nhánh mượn tạm
    /// GridTypeId của cha khi bản thân không có capDienAp riêng (xem InfrastructureRepository.UpsertFromPmisAsync).</summary>
    private static UpsertInfrastructureFromPmisRequest BuildLineUpsertRequest(PmisLineDto item) =>
        new()
        {
            InfraTypeId = 2,
            // .Trim() — xem giải thích ở SyncEquipmentAsync (mã lệch khoảng trắng khiến so khớp PMIS_CODE sai).
            // ?? string.Empty: MaDuongDay khai báo non-nullable nhưng System.Text.Json vẫn có thể gán null
            // nếu PMIS trả JSON "maDuongDay": null rõ ràng (default initializer chỉ áp dụng khi field VẮNG
            // MẶT) — .Trim() trần trước đây từng NRE ở tình huống này, khôi phục đúng hành vi null-tolerant
            // cũ, chỉ thêm chuẩn hoá khoảng trắng.
            PmisCode = item.MaDuongDay?.Trim() ?? string.Empty,
            Code = item.MaDuongDay?.Trim() ?? string.Empty,
            Name = item.TenDuongDay,
            UnitCode = item.MaDonVi,
            OperationDate = item.NgayVanHanh,
            GridTypeId = ResolveGridTypeId(item.CapDienAp),
            ParentPmisCode = ResolveParentLinePmisCode(item),
            CmisCode = item.MaCMIS
        };

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(
        int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItemsRaw, bool syncDocuments = true)
    {
        // Cô lập lỗi Deserialize THEO TỪNG BẢN GHI — TRƯỚC ĐÂY 1 bản ghi dị dạng (kiểu dữ liệu PMIS trả
        // sai, vd số bị trả dạng chuỗi) ném JsonException thẳng ra khỏi TOÀN BỘ trang (khỏi cả .Select()
        // lẫn vòng for bên dưới), khiến CẢ TRANG bị PushPageAsync ở PmisScheduledSyncJob bắt và đánh Failed
        // hết — kể cả các bản ghi hợp lệ khác cùng trang — và vì phân trang dùng skip cố định, lỗi này LẶP
        // LẠI Y HỆT mỗi lượt đồng bộ tiếp theo, "khoá" cả trang vĩnh viễn. Giờ lọc bỏ đúng bản ghi lỗi ra
        // khỏi rawItems TRƯỚC khi xử lý, ghi 1 SyncHistoryDetail Failed riêng cho nó, các bản ghi còn lại
        // trong trang xử lý bình thường.
        var rawItems = new List<JsonElement>(rawItemsRaw.Count);
        var deserializeFailedDetails = new List<SyncHistoryDetail>();
        var deserializeFailedErrors = new List<string>();
        foreach (var raw in rawItemsRaw)
        {
            try
            {
                _ = infraTypeId == 1 ? raw.Deserialize<PmisSubstationDto>(JsonOptions) : (object?)raw.Deserialize<PmisLineDto>(JsonOptions);
                rawItems.Add(raw);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SyncInfrastructureAsync: bỏ qua 1 bản ghi {InfraType} không đọc được (JSON dị dạng/lệch kiểu dữ liệu).", infraTypeId == 1 ? "Trạm biến áp" : "Đường dây");
                deserializeFailedErrors.Add($"1 bản ghi {(infraTypeId == 1 ? "Trạm biến áp" : "Đường dây")} không đọc được: {SyncErrorFormatter.FormatShort(ex)}");
                deserializeFailedDetails.Add(new SyncHistoryDetail
                {
                    SyncHistoryId = syncHistoryId,
                    SourceId = null,
                    SourceCode = "(không đọc được)",
                    SourceName = null,
                    TargetId = null,
                    ActionType = SyncActionType.Skip,
                    Status = SyncDetailStatus.Failed,
                    DataContent = raw.GetRawText(),
                    ErrorMessage = $"Không đọc được dữ liệu JSON từ PMIS: {SyncErrorFormatter.FormatShort(ex)}"
                });
            }
        }

        List<UpsertInfrastructureFromPmisRequest> upsertRequests;
        List<UpsertInfrastructureFromPmisResult> results;

        if (infraTypeId == 1)
        {
            upsertRequests = rawItems.Select(raw =>
            {
                var item = raw.Deserialize<PmisSubstationDto>(JsonOptions)!;
                return new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 1,
                    // .Trim() — xem giải thích ở SyncEquipmentAsync (mã lệch khoảng trắng khiến so khớp PMIS_CODE sai).
                    // ?? string.Empty — xem giải thích ở BuildLineUpsertRequest (PMIS có thể trả null rõ
                    // ràng dù C# khai báo non-nullable).
                    PmisCode = item.MaTBA?.Trim() ?? string.Empty,
                    Code = item.MaTBA?.Trim() ?? string.Empty,
                    Name = item.TenTBA,
                    Address = item.DiaDiem,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh,
                    GridTypeId = ResolveGridTypeId(item.CapDienAp),
                    CmisCode = item.MaCMIS
                };
            }).ToList();

            results = upsertRequests.Count == 0
                ? []
                : await UpsertInfrastructureSafeAsync(upsertRequests);
        }
        else
        {
            // Đường dây: xử lý TRỤC (ResolveParentLinePmisCode = null) trước, NHÁNH (có mã cha) sau — trong
            // CÙNG 1 trang này, để 1 trục MỚI (chưa từng tồn tại) được INSERT xong trước khi nhánh của chính nó (nếu cùng trang)
            // tới lượt — EquipmentService tự SELECT theo PMIS_CODE của item.ParentPmisCode ngay trong lúc
            // xử lý nhánh (xem InfrastructureRepository.UpsertFromPmisAsync), nên chỉ cần đảm bảo THỨ TỰ
            // gọi, không cần tự dò/cache Id nào ở tầng này nữa (khác trước 2026-09-23, khi còn phải tự tra
            // theo tên qua _lineNameIndex). Nếu trục nằm ở TRANG SAU (hoặc lượt sau), nhánh vẫn tạm thời
            // PARENT_ID=null/cũ và tự khớp đúng ở lượt đồng bộ kế tiếp (PMIS trả toàn bộ dữ liệu mỗi lượt).
            // Vẫn build lại đúng theo THỨ TỰ GỐC của rawItems ở cuối để details/rawItems[i] bên dưới khớp chỉ số.
            var lineItems = new PmisLineDto[rawItems.Count];
            var rootIndices = new List<int>();
            var branchIndices = new List<int>();
            for (var i = 0; i < rawItems.Count; i++)
            {
                var item = rawItems[i].Deserialize<PmisLineDto>(JsonOptions)!;
                lineItems[i] = item;
                (ResolveParentLinePmisCode(item) == null ? rootIndices : branchIndices).Add(i);
            }

            var requestsByIndex = new UpsertInfrastructureFromPmisRequest?[rawItems.Count];
            var resultsByIndex = new UpsertInfrastructureFromPmisResult?[rawItems.Count];

            if (rootIndices.Count > 0)
            {
                var rootRequests = rootIndices.Select(i => BuildLineUpsertRequest(lineItems[i])).ToList();
                var rootResults = await UpsertInfrastructureSafeAsync(rootRequests);
                for (var k = 0; k < rootIndices.Count; k++)
                {
                    var idx = rootIndices[k];
                    requestsByIndex[idx] = rootRequests[k];
                    resultsByIndex[idx] = rootResults[k];
                }
            }

            if (branchIndices.Count > 0)
            {
                var branchRequests = branchIndices.Select(i => BuildLineUpsertRequest(lineItems[i])).ToList();
                var branchResults = await UpsertInfrastructureSafeAsync(branchRequests);
                for (var k = 0; k < branchIndices.Count; k++)
                {
                    var idx = branchIndices[k];
                    requestsByIndex[idx] = branchRequests[k];
                    resultsByIndex[idx] = branchResults[k];
                }
            }

            upsertRequests = requestsByIndex.Select(r => r!).ToList();
            results = resultsByIndex.Select(r => r!).ToList();
        }

        if (upsertRequests.Count == 0)
        {
            if (deserializeFailedDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(deserializeFailedDetails);
            return (0, deserializeFailedDetails.Count, 0, deserializeFailedErrors);
        }

        var details = new List<SyncHistoryDetail>(deserializeFailedDetails);
        var errors = new List<string>(deserializeFailedErrors);
        var successCount = 0;
        var parentUnresolvedWarnings = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.Success) successCount++;
            else errors.Add($"{result.PmisCode}: {result.ErrorMessage}");

            // ParentUnresolved: dòng Đường dây tự lưu THÀNH CÔNG nhưng maCha không khớp được đường trục
            // nào (chưa đồng bộ tới, mã sai, hoặc tự trỏ về chính nó — xem
            // InfrastructureRepository.UpsertFromPmisAsync) — ghi Warning thay vì Success để admin thấy
            // ngay trong "Lịch sử đồng bộ", KHÔNG tính là Failed (bản thân dòng vẫn lưu đúng, chỉ riêng cha
            // chưa xác định được, tự khớp lại ở lượt sau nếu do trục chưa đồng bộ tới).
            var isParentUnresolved = result.Success && result.ParentUnresolved;
            if (isParentUnresolved) parentUnresolvedWarnings++;

            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = result.PmisCode,
                SourceCode = result.PmisCode,
                SourceName = upsertRequests[i].Name,
                TargetId = result.InfrastructureId?.ToString(),
                ActionType = !result.HasChanged ? SyncActionType.Skip : (result.WasCreated ? SyncActionType.Create : SyncActionType.Update),
                Status = !result.Success ? SyncDetailStatus.Failed : isParentUnresolved ? SyncDetailStatus.Warning : SyncDetailStatus.Success,
                DataContent = rawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage ?? (isParentUnresolved
                    ? $"Chưa xác định được đường dây cha (mã PMIS cha '{upsertRequests[i].ParentPmisCode}' chưa đồng bộ tới hoặc không hợp lệ) — tự khớp lại ở lượt đồng bộ kế tiếp."
                    : null)
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);

        // Đồng bộ tài liệu đính kèm (API 8/9) cho từng Trạm/Đường dây vừa lưu thành công — lỗi ở bước này
        // CHỈ ghi cảnh báo, không ảnh hưởng successCount/errors ở trên (xem SyncDocumentsForOwnerAsync).
        // syncDocuments=false (luồng AUTO/PmisScheduledSyncJob): BỎ QUA hẳn ở đây, chạy pass riêng có
        // rotation sau khi phân trang chính xong — xem SyncDocumentsForInfrastructureOwnerAsync +
        // PmisScheduledSyncJob.SyncDocumentsRotatingAsync. Luồng Manual giữ nguyên hành vi cũ (inline).
        var warnings = parentUnresolvedWarnings;
        if (syncDocuments)
        {
            var docDetails = new List<SyncHistoryDetail>();
            for (var i = 0; i < results.Count; i++)
            {
                if (!results[i].Success) continue;
                var req = upsertRequests[i];
                var isSubstationOrigin = req.InfraTypeId == 1;
                var (w, d) = await SyncDocumentsForOwnerAsync(
                    ownerType: "INFRASTRUCTURE",
                    ownerPmisCode: req.PmisCode,
                    sourceName: req.Name,
                    isSubstationOrigin: isSubstationOrigin,
                    maTBA: isSubstationOrigin ? req.PmisCode : null,
                    maDuongDay: isSubstationOrigin ? null : req.PmisCode,
                    maTB: null,
                    syncHistoryId: syncHistoryId);
                warnings += w;
                docDetails.AddRange(d);
            }
            if (docDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(docDetails);
        }

        return (successCount, (results.Count - successCount) + deserializeFailedDetails.Count, warnings, errors);
    }

    /// <summary>Trả false (và log cảnh báo đúng 1 lần khi chạm trần) nếu đã đạt <see cref="MaxEquipmentDetailCallsPerRun"/>
    /// — xem giải thích rủi ro ở khai báo hằng số. Gọi ngay TRƯỚC mỗi lượt gọi PMIS thật (ChiTietThietBi
    /// hoặc tải QR), không phải sau — tính đúng số round-trip PMIS thật đã/sẽ thực hiện.</summary>
    private bool TryConsumeEquipmentDetailCallBudget()
    {
        if (_equipmentDetailCallsThisRun >= MaxEquipmentDetailCallsPerRun)
            return false;

        _equipmentDetailCallsThisRun++;
        if (_equipmentDetailCallsThisRun == MaxEquipmentDetailCallsPerRun)
        {
            Log.Warning("PmisSyncExecutionService: đồng bộ Thiết bị đạt trần {Max} lượt gọi PMIS (ChiTietThietBi/QR) trong lượt này — các thiết bị TBA còn lại vẫn được lưu nhưng thiếu thông số kỹ thuật/QR mới, sẽ tự enrich lại ở lượt sau.", MaxEquipmentDetailCallsPerRun);
        }
        return true;
    }

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(
        string syncHistoryId, IReadOnlyList<JsonElement> rawItems, string? parentPmisCodeFallback = null)
    {
        var upsertRequests = new List<UpsertEquipmentFromPmisRequest>();
        // Song song 1:1 với upsertRequests — giữ lại ngữ cảnh gốc (TBA hay đường dây, mã cha) để đồng bộ
        // tài liệu đính kèm (API 8/9) đúng đối tượng sau khi thiết bị đã lưu thành công.
        var origins = new List<(bool IsSubstationOrigin, string? MaTBA, string? MaDuongDay, string MaTB)>();
        // Song song 1:1 với upsertRequests/origins (KHÔNG phải rawItems gốc nữa) — dùng để lấy lại
        // DataContent khi ghi SyncHistoryDetail bên dưới, sau khi đã lọc bỏ các bản ghi lỗi Deserialize.
        var validRawItems = new List<JsonElement>();
        // Cô lập lỗi Deserialize THEO TỪNG THIẾT BỊ — cùng lý do với SyncInfrastructureAsync: TRƯỚC ĐÂY 1
        // thiết bị có dữ liệu PMIS dị dạng ném JsonException ra khỏi CẢ foreach, làm cả trang bị đánh
        // Failed hết và lặp lại y hệt mỗi lượt (skip cố định). Giờ bỏ qua đúng thiết bị lỗi, ghi Detail
        // Failed riêng, các thiết bị còn lại trong trang xử lý bình thường.
        var deserializeFailedDetails = new List<SyncHistoryDetail>();
        var deserializeFailedErrors = new List<string>();
        foreach (var raw in rawItems)
        {
            EquipmentSaveShape item;
            try
            {
                item = raw.Deserialize<EquipmentSaveShape>(JsonOptions)!;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SyncEquipmentAsync: bỏ qua 1 bản ghi Thiết bị không đọc được (JSON dị dạng/lệch kiểu dữ liệu).");
                deserializeFailedErrors.Add($"1 bản ghi Thiết bị không đọc được: {SyncErrorFormatter.FormatShort(ex)}");
                deserializeFailedDetails.Add(new SyncHistoryDetail
                {
                    SyncHistoryId = syncHistoryId,
                    SourceId = null,
                    SourceCode = "(không đọc được)",
                    SourceName = null,
                    TargetId = null,
                    ActionType = SyncActionType.Skip,
                    Status = SyncDetailStatus.Failed,
                    DataContent = raw.GetRawText(),
                    ErrorMessage = $"Không đọc được dữ liệu JSON từ PMIS: {SyncErrorFormatter.FormatShort(ex)}"
                });
                continue;
            }
            validRawItems.Add(raw);

            // Thiết bị TBA (nhận diện bằng MaThietBi có giá trị — chỉ dạng thiết bị này mới có field
            // này, xem PmisSubstationDeviceDto) không có sẵn MaQRCode trong danh sách (ThongSoKyThuat/
            // TenThongSoKyThuat thì đã có từ 2026-09-23, xem PmisSubstationDeviceDto) — vẫn phải gọi
            // thêm ChiTietThietBi (API 7) ngay tại đây để lấy QR, tự động trong lúc đồng bộ, không chờ
            // người dùng bấm gì thêm; tiện thể ChiTietThietBi cũng trả ThongSoKyThuat/TenThongSoKyThuat
            // nên vẫn dùng luôn kết quả đó (ưu tiên dữ liệu mới nhất) thay vì giá trị đã có sẵn ở trên.
            var isSubstationDevice = !string.IsNullOrWhiteSpace(item.MaThietBi);
            // .Trim() — PMIS đôi khi trả mã kèm khoảng trắng thừa; PmisCode/ParentPmisCode dùng để SO KHỚP
            // (WHERE PMIS_CODE = ...) nên lệch 1 khoảng trắng cũng khiến hệ thống hiểu nhầm là thiết bị/trạm
            // MỚI hoặc "đã chuyển" — đã gặp thật (1 trạm bị hiểu nhầm toàn bộ thiết bị "chuyển TBA", xem
            // Migration0060).
            var maTB = (isSubstationDevice ? item.MaThietBi : item.MaTB)?.Trim() ?? string.Empty;
            var tenTB = isSubstationDevice ? item.TenThietBi! : item.TenTB;
            var thongSoKyThuat = item.ThongSoKyThuat;
            var tenThongSoKyThuat = item.TenThongSoKyThuat;
            var maQRCode = item.MaQRCode;

            if (isSubstationDevice)
            {
                if (TryConsumeEquipmentDetailCallBudget())
                {
                    try
                    {
                        var detail = await _pmisClient.GetDeviceDetailAsync(new PmisDeviceDetailRequest
                        {
                            MaThietBi = maTB,
                            MaTBA = item.MaTBA
                        });
                        thongSoKyThuat = detail?.ThongSoKyThuat;
                        // "?? tenThongSoKyThuat" (KHÔNG dùng "??=" — cố tình giữ so sánh tường minh): nếu
                        // ChiTietThietBi gọi thành công nhưng bản thân field TenThongSoKyThuat rỗng (API 7
                        // chưa được PMIS cập nhật đồng bộ với API 4/6, hoặc thiếu cho riêng thiết bị này),
                        // KHÔNG được ghi đè mất giá trị đã có sẵn từ danh sách (item.TenThongSoKyThuat) —
                        // EquipmentPmisSpecRepository.UpsertAsync ghi đè FieldLabels vô điều kiện mỗi lần
                        // đồng bộ nên 1 lần ghi đè bằng null ở đây sẽ xoá mất nhãn tốt đã lưu từ trước.
                        tenThongSoKyThuat = detail?.TenThongSoKyThuat ?? tenThongSoKyThuat;
                        maQRCode = detail?.MaQRCode;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "PmisSyncExecutionService: lỗi gọi ChiTietThietBi cho thiết bị TBA {MaThietBi}, bỏ qua thông số kỹ thuật/QR.", maTB);
                    }
                }
            }

            // maQRCode PMIS trả về là URL (vd. ".../AnhQRCode?idPmis=..."), không phải base64 — phải tải
            // ảnh nhị phân thật rồi tự encode base64 mới lưu đúng vào EQUIPMENTS.QR_CODE (giữ nguyên
            // field/cột cũ, chỉ sửa cách lấy giá trị).
            string? qrCodeBase64 = null;
            if (!string.IsNullOrWhiteSpace(maQRCode) && TryConsumeEquipmentDetailCallBudget())
            {
                try
                {
                    var bytes = await _pmisClient.GetDeviceQrImageBytesAsync(maTB);
                    if (bytes is { Length: > 0 }) qrCodeBase64 = Convert.ToBase64String(bytes);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "PmisSyncExecutionService: lỗi tải ảnh QR cho thiết bị {MaTB}, bỏ qua QR.", maTB);
                }
            }

            // Chỉ thiết bị TBA có capDienAp riêng — thiết bị đường dây để null, EquipmentRepository sẽ
            // tự lấy GridTypeId của đường dây cha làm phương án dự phòng.
            var gridTypeId = isSubstationDevice ? ResolveGridTypeId(item.CapDienAp) : null;

            // Ưu tiên tên loại thiết bị CHUẨN từ danh mục PMIS (API 3/5) — chỉ thật sự được dùng khi hệ
            // thống lần đầu gặp loại thiết bị này và phải tự tạo EquipmentTypes mới (xem
            // ResolveOrCreateEquipmentTypeIdAsync), còn không thì bị bỏ qua vì loại đã tồn tại từ trước.
            // Fallback về tenLoaiTB đính kèm dòng thiết bị nếu danh mục không có mã này hoặc PMIS lỗi.
            var equipmentTypeName = await ResolveDeviceTypeNameAsync(isSubstationDevice, item.MaLoaiTB) ?? item.TenLoaiTB;

            // Fallback về parentPmisCodeFallback (cha đã biết chắc chắn từ ngữ cảnh gọi — vd. đang lặp
            // API 5/6 theo ĐÚNG 1 trạm/đường dây, hoặc người dùng đã chọn trạm/đường dây khi tìm kiếm
            // thủ công) khi PMIS trả về dòng thiết bị THIẾU (null HOẶC rỗng — xem BlankToNull) field
            // maTBA/maDuongDay — đã gặp thật, khiến ParentPmisCode=null và INFRASTRUCTURE_ID bị ghi NULL
            // lúc INSERT lần đầu (không rơi vào nhánh update còn fallback về InfrastructureId cũ), làm
            // thiết bị "đồng bộ Thành công" nhưng biến mất khỏi danh sách thiết bị của trạm/đường dây
            // (EquipmentRepository.GetPagedAsync lọc theo INFRASTRUCTURE_ID). Xem
            // pmis_sync_equipment_orphan_null_infra_id trong memory.
            var resolvedParentPmisCode = (BlankToNull(item.MaTBA) ?? BlankToNull(item.MaDuongDay) ?? parentPmisCodeFallback)?.Trim();

            upsertRequests.Add(new UpsertEquipmentFromPmisRequest
            {
                PmisCode = maTB,
                Code = maTB,
                Name = tenTB,
                EquipmentTypeCode = item.MaLoaiTB ?? string.Empty,
                EquipmentTypeName = equipmentTypeName,
                ParentPmisCode = resolvedParentPmisCode,
                UnitCode = item.MaDonVi,
                ManufactureYear = item.NamSanXuat,
                QrCodeBase64 = qrCodeBase64,
                GridTypeId = gridTypeId,
                ThongSoKyThuat = thongSoKyThuat,
                TenThongSoKyThuat = tenThongSoKyThuat
            });
            // Dùng ĐÚNG resolvedParentPmisCode (không phải item.MaTBA/MaDuongDay thô) cho origins — nếu
            // không, đồng bộ tài liệu đính kèm (SyncDocumentsForOwnerAsync ngay dưới) vẫn nhận mã cha
            // null/rỗng cho đúng những thiết bị vừa được cứu khỏi orphan ở trên, khiến API 8/9 luôn thất
            // bại/rỗng cho riêng các thiết bị này dù thiết bị đã được gán đúng trạm/đường dây.
            origins.Add((isSubstationDevice,
                isSubstationDevice ? resolvedParentPmisCode : null,
                isSubstationDevice ? null : resolvedParentPmisCode,
                maTB));
        }

        if (upsertRequests.Count == 0)
        {
            if (deserializeFailedDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(deserializeFailedDetails);
            return (0, deserializeFailedDetails.Count, 0, deserializeFailedErrors);
        }

        var results = await UpsertEquipmentSafeAsync(upsertRequests);

        var details = new List<SyncHistoryDetail>(deserializeFailedDetails);
        var errors = new List<string>(deserializeFailedErrors);
        var successCount = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.Success) successCount++;
            else errors.Add($"{result.PmisCode}: {result.ErrorMessage}");

            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = result.PmisCode,
                SourceCode = result.PmisCode,
                SourceName = upsertRequests[i].Name,
                TargetId = result.EquipmentId?.ToString(),
                ActionType = !result.HasChanged ? SyncActionType.Skip : (result.WasCreated ? SyncActionType.Create : SyncActionType.Update),
                Status = result.Success ? SyncDetailStatus.Success : SyncDetailStatus.Failed,
                DataContent = validRawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);

        // Đồng bộ tài liệu đính kèm (API 8/9) cho từng thiết bị vừa lưu thành công — theo đúng nguyên
        // tắc "lỗi ở đây chỉ cảnh báo, không ảnh hưởng successCount/errors ở trên" (xem
        // SyncDocumentsForOwnerAsync).
        var warnings = 0;
        var docDetails = new List<SyncHistoryDetail>();
        for (var i = 0; i < results.Count; i++)
        {
            if (!results[i].Success) continue;
            var origin = origins[i];
            var (w, d) = await SyncDocumentsForOwnerAsync(
                ownerType: "EQUIPMENT",
                ownerPmisCode: origin.MaTB,
                sourceName: upsertRequests[i].Name,
                isSubstationOrigin: origin.IsSubstationOrigin,
                maTBA: origin.MaTBA,
                maDuongDay: origin.MaDuongDay,
                maTB: origin.MaTB,
                syncHistoryId: syncHistoryId);
            warnings += w;
            docDetails.AddRange(d);
        }
        if (docDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(docDetails);

        return (successCount, (results.Count - successCount) + deserializeFailedDetails.Count, warnings, errors);
    }

    /// <summary>
    /// Đồng bộ tài liệu đính kèm (API 8 SUBSTATION_DOCUMENT_LIST / API 9 LINE_DOCUMENT_LIST) cho 1 Trạm/
    /// Đường dây/Thiết bị đã lưu thành công — tải file thật qua URL PMIS trả về, gửi base64 sang
    /// EquipmentService để lưu MinIO. Lỗi ở BẤT KỲ bước nào (gọi API danh sách, tải file, lưu) đều CHỈ
    /// tạo dòng SyncHistoryDetail trạng thái Warning — không throw ra ngoài, không được cộng vào
    /// successCount/errors của bản ghi chính (Trạm/Đường dây/Thiết bị đã lưu xong trước khi gọi hàm này).
    /// </summary>
    /// <summary>Wrapper công khai cho <see cref="SyncDocumentsForOwnerAsync"/> — dùng bởi
    /// PmisScheduledSyncJob.SyncDocumentsRotatingAsync (pass riêng có rotation, xem
    /// IPmisSyncExecutionService.SyncDocumentsForInfrastructureOwnerAsync). Chỉ có "mã PMIS + loại" (không
    /// có tên) vì nguồn dữ liệu là InfrastructureRepository.GetSyncedPmisCodesAsync (rẻ, không SELECT tên)
    /// — SourceName trong SyncHistoryDetail dùng tạm chính mã PMIS.</summary>
    public Task<(int Warnings, List<SyncHistoryDetail> Details)> SyncDocumentsForInfrastructureOwnerAsync(
        string ownerPmisCode, int infraTypeId, string syncHistoryId)
    {
        var isSubstationOrigin = infraTypeId == 1;
        return SyncDocumentsForOwnerAsync(
            ownerType: "INFRASTRUCTURE",
            ownerPmisCode: ownerPmisCode,
            sourceName: ownerPmisCode,
            isSubstationOrigin: isSubstationOrigin,
            maTBA: isSubstationOrigin ? ownerPmisCode : null,
            maDuongDay: isSubstationOrigin ? null : ownerPmisCode,
            maTB: null,
            syncHistoryId: syncHistoryId);
    }

    private async Task<(int Warning, List<SyncHistoryDetail> Details)> SyncDocumentsForOwnerAsync(
        string ownerType, string ownerPmisCode, string sourceName, bool isSubstationOrigin,
        string? maTBA, string? maDuongDay, string? maTB, string syncHistoryId)
    {
        var details = new List<SyncHistoryDetail>();

        // Đạt trần MaxDocumentSyncCallsPerRun — bỏ qua HẲN owner này (không gọi PMIS), caller
        // (PmisScheduledSyncJob) sẽ dừng phân trang tại đây và lưu SyncConfig.SyncCursor để tiếp tục đúng
        // owner này ở lượt sau, không mất dữ liệu.
        if (!TryConsumeDocumentSyncCallBudget())
        {
            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = ownerPmisCode,
                SourceCode = ownerPmisCode,
                SourceName = sourceName,
                ActionType = SyncActionType.Skip,
                Status = SyncDetailStatus.Warning,
                ErrorMessage = $"Chưa đồng bộ tài liệu đính kèm — lượt này đã đạt trần {MaxDocumentSyncCallsPerRun} owner, sẽ tự tiếp tục ở lượt sau."
            });
            return (1, details);
        }

        try
        {
            var pageSize = await GetPageSizeAsync(isSubstationOrigin ? "SUBSTATION_DOCUMENT_LIST" : "LINE_DOCUMENT_LIST");
            var items = new List<(string MaTaiLieu, string? TenTaiLieu, string? LoaiTaiLieu, string? File, string? MaTB)>();
            var skip = 0;
            var hitRecordCap = false;
            while (true)
            {
                if (isSubstationOrigin)
                {
                    var resp = await _pmisClient.GetSubstationDocumentsAsync(new PmisSubstationDocumentSearchRequest
                    {
                        MaTBA = maTBA,
                        MaTB = maTB,
                        Skip = skip,
                        Take = pageSize
                    });
                    items.AddRange(resp.Items.Select(d => (d.MaTaiLieu, d.TenTaiLieu, d.LoaiTaiLieu, d.File, d.MaTB)));
                    if (resp.Items.Count < pageSize || items.Count >= resp.Total) break;
                }
                else
                {
                    var resp = await _pmisClient.GetLineDocumentsAsync(new PmisLineDocumentSearchRequest
                    {
                        MaDuongDay = maDuongDay,
                        MaTB = maTB,
                        Skip = skip,
                        Take = pageSize
                    });
                    items.AddRange(resp.Items.Select(d => (d.MaTaiLieu, d.TenTaiLieu, d.LoaiTaiLieu, d.File, d.MaTB)));
                    if (resp.Items.Count < pageSize || items.Count >= resp.Total) break;
                }
                skip += pageSize;

                if (skip >= DocumentMaxTotalRecords)
                {
                    Log.Warning("PmisSyncExecutionService: tài liệu của {OwnerType} {OwnerPmisCode} đã đạt giới hạn an toàn {Max} bản ghi/lượt, dừng lại dù PMIS có thể còn dữ liệu (skip={Skip}).",
                        ownerType, ownerPmisCode, DocumentMaxTotalRecords, skip);
                    hitRecordCap = true;
                    break;
                }
            }

            if (items.Count == 0) return (hitRecordCap ? 1 : 0, details);

            // Gửi lỗi 1 lô KHÔNG được làm mất kết quả của các lô trước đã gửi thành công — mỗi lô tự bắt
            // lỗi riêng và báo Warning cho đúng các tài liệu trong lô đó, thay vì để exception bay lên
            // catch ngoài cùng (vốn chỉ tạo 1 dòng cảnh báo chung, xoá mất kết quả các lô đã xong).
            async Task SendBatchAsync(List<UpsertPmisDocumentRequest> batch, List<UpsertPmisDocumentResult> sink)
            {
                if (batch.Count == 0) return;
                try
                {
                    sink.AddRange(await _equipmentServiceClient.UpsertDocumentsAsync(batch));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "PmisSyncExecutionService: lỗi gửi 1 lô tài liệu cho {OwnerType} {OwnerPmisCode}.", ownerType, ownerPmisCode);
                    sink.AddRange(batch.Select(b => new UpsertPmisDocumentResult
                    {
                        PmisDocumentCode = b.PmisDocumentCode,
                        Success = false,
                        ErrorMessage = $"Lỗi gửi lô tài liệu: {ex.Message}"
                    }));
                }
            }

            var endpointApiCode = isSubstationOrigin ? "SUBSTATION_DOCUMENT_LIST" : "LINE_DOCUMENT_LIST";
            var results = new List<UpsertPmisDocumentResult>();
            var requests = new List<UpsertPmisDocumentRequest>();
            // Chi tiết riêng từng tài liệu (tên, loại, URL file thật, kích thước tải được) để đưa vào
            // SyncHistoryDetail.DataContent — trước đây "Lịch sử đồng bộ" chỉ hiện mã/tên của TRẠM/ĐƯỜNG
            // DÂY (owner) lặp lại y hệt cho mọi tài liệu, không cách nào phân biệt tài liệu nào với tài
            // liệu nào, cũng không thấy được URL/kích thước file đã tải hay lỗi tải file thật sự (khác lỗi
            // lưu bản ghi ở EquipmentService) — xem PmisSyncExecutionService.cs (feedback người dùng
            // 2026-09-23: "thiếu log chi tiết cho api tải file vật lý").
            var docInfoByCode = new Dictionary<string, (string? TenTaiLieu, string? LoaiTaiLieu, string? FileUrl, int? FileSizeBytes, string? FileDownloadError)>();
            foreach (var doc in items)
            {
                if (string.IsNullOrWhiteSpace(doc.MaTaiLieu)) continue;

                string? fileBase64 = null;
                string? fileDownloadError = null;
                int? fileSizeBytes = null;
                if (!string.IsNullOrWhiteSpace(doc.File))
                {
                    var (bytes, errorReason) = await _pmisClient.DownloadDocumentFileAsync(doc.File, endpointApiCode);
                    if (bytes is { Length: > 0 })
                    {
                        fileBase64 = Convert.ToBase64String(bytes);
                        fileSizeBytes = bytes.Length;
                    }
                    else fileDownloadError = errorReason;
                }
                else
                {
                    // Phân biệt rõ với trường hợp CÓ URL nhưng tải lỗi (fileDownloadError ở trên, có
                    // "Nguyên nhân: HTTP 404/timeout/..." cụ thể) — ở đây PMIS trả về tài liệu này nhưng
                    // KHÔNG kèm URL file (trường "File" rỗng/null), nên SyncService chưa từng gọi HTTP.
                    // Không phải lỗi kết nối phía hệ thống này — khả năng cao PMIS chưa đính kèm file cho
                    // bản ghi tài liệu này.
                    fileDownloadError = "PMIS không trả về URL file cho tài liệu này (trường \"File\" rỗng) — chưa từng thử tải.";
                }

                docInfoByCode[doc.MaTaiLieu] = (doc.TenTaiLieu, doc.LoaiTaiLieu, doc.File, fileSizeBytes, fileDownloadError);

                requests.Add(new UpsertPmisDocumentRequest
                {
                    PmisDocumentCode = doc.MaTaiLieu,
                    OwnerType = ownerType,
                    OwnerPmisCode = ownerPmisCode,
                    DocumentName = doc.TenTaiLieu,
                    DocumentType = doc.LoaiTaiLieu,
                    FileName = doc.TenTaiLieu ?? doc.MaTaiLieu,
                    FileBase64 = fileBase64,
                    FileDownloadError = fileDownloadError,
                    SyncHistoryId = syncHistoryId,
                    // Đồng bộ cấp Trạm/Đường dây (maTB tham số = null, không lọc) PMIS trả về CẢ tài liệu
                    // thuộc riêng 1 thiết bị con (doc.MaTB có giá trị) LẪN tài liệu thuộc chính trạm/đường
                    // dây — luôn gửi kèm doc.MaTB để EquipmentService tự ưu tiên gán đúng OwnerType=
                    // EQUIPMENT nếu thiết bị đó đã tồn tại (xem InternalPmisSyncController.UpsertDocumentsFromPmis),
                    // KHÔNG tự bỏ qua tài liệu ở đây — thiết bị chưa tồn tại thì vẫn giữ được tài liệu
                    // (gán tạm theo OwnerType/OwnerPmisCode ở trên) thay vì mất hẳn.
                    DeviceCode = doc.MaTB
                });

                // Gửi theo lô cố định thay vì gộp hết rồi gửi 1 request duy nhất ở cuối — tránh 1 owner
                // có nhiều tài liệu thật (base64 hoá) tạo ra request khổng lồ dễ vượt timeout/OOM.
                if (requests.Count >= DocumentUpsertBatchSize)
                {
                    await SendBatchAsync(requests, results);
                    requests.Clear();
                }
            }

            await SendBatchAsync(requests, results);

            if (results.Count == 0) return (hitRecordCap ? 1 : 0, details);

            var warningCount = 0;
            foreach (var result in results)
            {
                docInfoByCode.TryGetValue(result.PmisDocumentCode, out var info);

                // Tải file lỗi (info.FileDownloadError != null) vẫn có thể đi kèm result.Success=true phía
                // EquipmentService (bản ghi tài liệu vẫn lưu được, chỉ thiếu file) — trước đây trường hợp
                // này hiện "Thành công"/"---" y hệt 1 tài liệu tải file trót lọt, không ai biết file thật
                // sự chưa có (chỉ lộ ra sau, khi bấm tải về mới báo "Tài liệu chưa có file"). Coi đây là
                // Warning ngay từ lúc đồng bộ, không chờ tới lúc người dùng tự phát hiện.
                var isWarning = !result.Success || info.FileDownloadError != null;
                if (isWarning) warningCount++;

                // Dùng ĐÚNG khoá TenTBA/TenDuongDay (không bịa khoá "OwnerName" mới) — đây là 2 khoá mà
                // FE (getParentName) đã đọc sẵn từ dataContent của dòng Trạm/Đường dây/Thiết bị chính
                // (item PMIS thô, xem dòng ~726/912 dưới), nên dòng tài liệu tái dùng đúng quy ước đó,
                // FE không cần thêm nhánh đặc biệt nào cho riêng dòng tài liệu.
                object dataContentObj = isSubstationOrigin
                    ? new { info.TenTaiLieu, info.LoaiTaiLieu, OwnerType = ownerType, OwnerPmisCode = ownerPmisCode, TenTBA = sourceName, FileUrl = info.FileUrl, FileSizeBytes = info.FileSizeBytes, Downloaded = info.FileSizeBytes != null, result.WasSkippedAsExisting }
                    : new { info.TenTaiLieu, info.LoaiTaiLieu, OwnerType = ownerType, OwnerPmisCode = ownerPmisCode, TenDuongDay = sourceName, FileUrl = info.FileUrl, FileSizeBytes = info.FileSizeBytes, Downloaded = info.FileSizeBytes != null, result.WasSkippedAsExisting };
                var dataContent = JsonSerializer.Serialize(dataContentObj);

                details.Add(new SyncHistoryDetail
                {
                    SyncHistoryId = syncHistoryId,
                    SourceId = result.PmisDocumentCode,
                    SourceCode = result.PmisDocumentCode,
                    SourceName = info.TenTaiLieu ?? result.PmisDocumentCode,
                    DataContent = dataContent,
                    ActionType = SyncActionType.Skip,
                    Status = isWarning ? SyncDetailStatus.Warning : SyncDetailStatus.Success,
                    ErrorMessage = result.ErrorMessage ?? info.FileDownloadError
                });
            }
            return (hitRecordCap ? warningCount + 1 : warningCount, details);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi đồng bộ tài liệu cho {OwnerType} {OwnerPmisCode}.", ownerType, ownerPmisCode);
            details.Add(new SyncHistoryDetail
            {
                SyncHistoryId = syncHistoryId,
                SourceId = ownerPmisCode,
                SourceCode = ownerPmisCode,
                SourceName = sourceName,
                ActionType = SyncActionType.Skip,
                Status = SyncDetailStatus.Warning,
                ErrorMessage = $"Lỗi đồng bộ tài liệu đính kèm: {ex.Message}"
            });
            return (1, details);
        }
    }

    /// <summary>
    /// Suy ra loại lưới điện (1 = Cao áp, 2 = Trung áp, 3 = Hạ áp — 3 dòng của bảng GRIDTYPES sau
    /// Migration0053) từ chuỗi cấp điện áp PMIS trả về (vd. "110kV", "22kV", "0,4kV"). Ngưỡng theo đúng
    /// quy ước EVN: cao áp từ 66kV trở lên (66/110/220/500kV), trung áp từ 1kV đến dưới 66kV
    /// (6/10/15/22/35kV), hạ áp dưới 1kV (0,4kV / 0,22kV). Không parse được thì trả về null (giữ nguyên
    /// GridTypeId cũ khi update, không đoán bừa).
    /// </summary>
    internal static int? ResolveGridTypeId(string? capDienAp)
    {
        if (string.IsNullOrWhiteSpace(capDienAp)) return null;

        // PMIS dùng dấu phẩy thập phân ("0,4kV") — quy về dấu chấm rồi parse theo InvariantCulture để
        // không phụ thuộc culture của máy chạy service.
        var match = System.Text.RegularExpressions.Regex.Match(capDienAp, @"\d+(?:[.,]\d+)?");
        if (!match.Success) return null;

        var normalized = match.Value.Replace(',', '.');
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var kv))
        {
            return null;
        }

        return kv switch
        {
            >= 66 => 1,
            >= 1 => 2,
            _ => 3
        };
    }

    /// <summary>
    /// Shape gộp — thiết bị TBA và đường dây có schema JSON THẬT khác nhau (xem
    /// PmisSubstationDeviceDto/PmisLineDeviceDto): thiết bị TBA dùng MaThietBi/TenThietBi, không có
    /// MaQRCode; thiết bị đường dây dùng MaTB/TenTB, có sẵn MaQRCode. Cả 2 phía đều đã có
    /// ThongSoKyThuat/TenThongSoKyThuat ngay trong danh sách (PMIS bổ sung cho TBA từ 2026-09-23).
    /// Khai báo đủ field của cả 2 phía (đều optional) rồi tự chọn nhánh đúng trong SyncEquipmentAsync
    /// theo MaThietBi có giá trị.
    /// </summary>
    private class EquipmentSaveShape
    {
        public string? MaThietBi { get; set; }
        public string? TenThietBi { get; set; }
        public string? CapDienAp { get; set; } // chỉ thiết bị TBA có field này
        public string MaTB { get; set; } = string.Empty;
        public string TenTB { get; set; } = string.Empty;
        public string? MaLoaiTB { get; set; }
        public string? TenLoaiTB { get; set; }
        public string? MaTBA { get; set; }
        public string? MaDuongDay { get; set; }
        public string? MaDonVi { get; set; }
        public int? NamSanXuat { get; set; }
        public string? MaQRCode { get; set; }
        public string? ThongSoKyThuat { get; set; }
        public string? TenThongSoKyThuat { get; set; }
    }
}
