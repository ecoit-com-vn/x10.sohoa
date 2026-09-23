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

    // An toàn: tối đa số đường dây cập nhật PARENT_ID/lượt backfill — UpdateParentIdsAsync (EquipmentService)
    // cập nhật TUẦN TỰ từng dòng qua Dapper (không phải 1 câu SQL gộp), nếu số "chưa xác định cha" lên tới
    // hàng nghìn (từng xảy ra thật trên production do 1 bug khác khiến hầu hết đường dây tạm thời rơi vào
    // trạng thái này) có thể mất hàng chục phút, khiến SyncHistoryWatchdogJob đánh rớt cả lượt vì treo
    // RUNNING quá 30 phút thật sự. Phần vượt trần tự thử tiếp ở các lượt sau, không mất dữ liệu.
    private const int MaxBackfillPerRun = 2000;

    // An toàn: giới hạn số lượt gọi PMIS THẬT (ChiTietThietBi + tải ảnh QR) trong 1 lượt đồng bộ Thiết bị —
    // mỗi lượt là 1 round-trip PMIS TUẦN TỰ (xem SyncEquipmentAsync), không giới hạn trước đây có thể khiến
    // 1 lượt xử lý hàng nghìn thiết bị TBA chạy quá lâu và bị SyncHistoryWatchdogJob đánh rớt vì treo
    // RUNNING quá 30 phút — CÙNG lớp rủi ro đã xảy ra thật với backfill cha đường dây (xem MaxBackfillPerRun
    // ở trên). Thiết bị vượt trần vẫn được lưu đầy đủ Code/Name/EquipmentTypeCode... chỉ thiếu
    // ThongSoKyThuat/QR mới của lượt này — PMIS trả về TOÀN BỘ dữ liệu mỗi lượt (không phải delta) nên
    // thiết bị đó tự được enrich lại ở lượt sau, không mất dữ liệu.
    private const int MaxEquipmentDetailCallsPerRun = 500;
    private int _equipmentDetailCallsThisRun;

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

    // Danh mục Đường dây hiện có (tên đã chuẩn hoá -> danh sách ứng viên [mã đơn vị PMIS, Id, GridTypeId
    // hiện có]) — tải 1 lần/lượt đồng bộ Đường dây, KHÔNG tải lại theo từng dòng — dùng để tự tìm cha theo
    // tên trong bộ nhớ thay vì mỗi dòng tự query DB riêng (xem ResolveParentLineIdAsync). Khoá bằng tên đã
    // chuẩn hoá (NormalizeLineName) để không nhạy khoảng trắng thừa/khoảng trắng kép giữa dòng trục và
    // dòng nhánh. GridTypeId đi kèm để nhánh có thể mượn tạm cấp điện áp của trục khi nhánh không có
    // capDienAp riêng — PMIS không trả capDienAp cho phần lớn nhánh, chỉ trục mới luôn có.
    private Dictionary<string, List<(string? PmisUnitCode, Guid Id, int? GridTypeId)>>? _lineNameIndex;

    // Chỉ cảnh báo LOG 1 lần cho mỗi tên trục bị trùng thật sự (không phân biệt được bằng mã đơn vị) —
    // tránh spam log khi 1 trục có nhiều nhánh con cùng gặp phải tình huống trùng tên đó. Đây là dấu hiệu
    // dữ liệu PMIS có vấn đề thật (2 trục khác nhau trùng tên), khác với "chưa tìm thấy vì trục chưa đồng
    // bộ tới trong lượt này" (tình huống tạm thời, tự hết sau 1-2 lượt, không cần log riêng mỗi tên).
    private readonly HashSet<string> _warnedAmbiguousParentNames = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Luôn trả về Dictionary (rỗng nếu EquipmentService lỗi) — cùng lý do với LoadDeviceTypeNamesAsync:
    /// rỗng thì chỉ thử tải đúng 1 lần/lượt, không lặp lại vô ích cho từng dòng đường dây tiếp theo.</summary>
    private async Task<Dictionary<string, List<(string? PmisUnitCode, Guid Id, int? GridTypeId)>>> LoadLineNameIndexAsync()
    {
        try
        {
            var entries = await _equipmentServiceClient.GetLineNameIndexAsync();
            var index = new Dictionary<string, List<(string?, Guid, int?)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
                AddLineToIndex(index, entry.Name, entry.PmisUnitCode, entry.Id, entry.GridTypeId);
            return index;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi khi tải danh mục Đường dây hiện có, bỏ qua việc gán cha-con đường dây cho cả lượt chạy này.");
            return new Dictionary<string, List<(string?, Guid, int?)>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Chuẩn hoá tên đường dây để so khớp: cắt khoảng trắng đầu/cuối và gộp khoảng trắng liên
    /// tiếp ở giữa thành 1 dấu cách — tránh lệch do lỗi nhập liệu thường gặp (thừa dấu cách) giữa dòng
    /// trục và phần tên nhánh tách ra từ chính nó. Null/rỗng trả về null.</summary>
    private static string? NormalizeLineName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var collapsed = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
        return collapsed.Length > 0 ? collapsed : null;
    }

    /// <summary>
    /// Tự tìm Id đường dây CHA theo tên (đã tách sẵn bởi ResolveParentLineName), tra qua danh mục tải 1
    /// lần/lượt đồng bộ (KHÔNG tự query DB — xem LoadLineNameIndexAsync). Trả về (IsRoot, ParentId,
    /// ParentGridTypeId) — ParentGridTypeId là GRIDTYPEID hiện có của đường trục cha vừa khớp được, để
    /// nhánh mượn tạm khi bản thân nhánh không có capDienAp riêng (xem BuildLineUpsertRequest):
    /// - Tên không có "/" → (true, null, null) — chắc chắn là đường trục gốc, không có cha.
    /// - Có "/" nhưng danh mục không có tên trục đó (trục chưa đồng bộ tới trong lượt này, hoặc lệch tên
    ///   dù đã chuẩn hoá) → (false, null, null) — caller giữ nguyên PARENT_ID cũ, không xoá.
    /// - Có "/" và khớp đúng 1 ứng viên (theo tên, hoặc theo tên + mã đơn vị nếu trùng tên nhiều nơi) →
    ///   (false, Id đó, GridTypeId đó).
    /// - Có "/" nhưng trùng tên ở ≥2 nơi KHÔNG phân biệt được bằng mã đơn vị (dữ liệu PMIS thật sự có tên
    ///   trục trùng nhau) → (false, null, null) + cảnh báo 1 lần/tên trục, không đoán đại 1 trong số đó.
    /// </summary>
    private (bool IsRoot, Guid? ParentId, int? ParentGridTypeId) ResolveParentLineId(string? tenDuongDay, string? maDonVi)
    {
        var parentNameRaw = ResolveParentLineName(tenDuongDay);
        if (parentNameRaw == null) return (true, null, null); // không có "/" -> chắc chắn là gốc

        var normalized = NormalizeLineName(parentNameRaw);
        if (normalized == null) return (false, null, null);

        return TryMatchLineByName(normalized, maDonVi, out var id, out var gridTypeId)
            ? (false, id, gridTypeId)
            : (false, null, null); // trục chưa có trong danh mục lượt này — giữ nguyên PARENT_ID cũ, tự khớp đúng ở lượt sau
    }

    /// <summary>Tra 1 tên Đường dây (đã NormalizeLineName) trong <see cref="_lineNameIndex"/>, tự phân biệt
    /// bằng mã đơn vị PMIS nếu trùng tên ở nhiều nơi — dùng bởi ResolveParentLineId (khớp cấp liền kề).</summary>
    private bool TryMatchLineByName(string normalizedName, string? maDonVi, out Guid id, out int? gridTypeId)
    {
        id = default;
        gridTypeId = null;
        if (_lineNameIndex == null || !_lineNameIndex.TryGetValue(normalizedName, out var candidates)) return false;

        if (candidates.Count == 1)
        {
            (_, id, gridTypeId) = candidates[0];
            return true;
        }

        // Trùng tên ở nhiều nơi — thử phân biệt bằng mã đơn vị PMIS của chính dòng đang xử lý.
        var sameUnit = candidates.Where(c => string.Equals(c.PmisUnitCode, maDonVi, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameUnit.Count == 1)
        {
            (_, id, gridTypeId) = sameUnit[0];
            return true;
        }

        if (_warnedAmbiguousParentNames.Add(normalizedName))
        {
            Log.Warning("PmisSyncExecutionService: tên đường trục '{ParentName}' trùng ở {Count} đường dây khác nhau, không phân biệt được bằng mã đơn vị — bỏ qua gán/tạo cha cho các nhánh tham chiếu tới tên này.",
                normalizedName, candidates.Count);
        }
        return false;
    }

    /// <summary>Đưa 1 đường trục VỪA lưu thành công (trong CHÍNH lượt đồng bộ đang chạy) vào
    /// <see cref="_lineNameIndex"/> ngay lập tức — để các nhánh tham chiếu tới nó xử lý SAU trong cùng
    /// lượt (cùng trang hoặc trang kế tiếp) tìm thấy được ngay, KHÔNG phải đợi tới lượt chạy kế tiếp mới
    /// tự khớp lại (xem SyncInfrastructureAsync — xử lý trục trước, nhánh sau, trong cùng 1 trang).</summary>
    private void AddLineToIndex(string? name, string? unitCode, Guid id, int? gridTypeId)
    {
        if (_lineNameIndex != null) AddLineToIndex(_lineNameIndex, name, unitCode, id, gridTypeId);
    }

    /// <summary>Chèn (hoặc gộp thêm ứng viên nếu trùng tên) 1 dòng vào danh mục tra cứu đường dây theo
    /// tên đã chuẩn hoá — dùng chung bởi LoadLineNameIndexAsync (tải cả danh mục 1 lần) và
    /// AddLineToIndex (bổ sung từng dòng mới tạo giữa lượt chạy).</summary>
    private static void AddLineToIndex(
        Dictionary<string, List<(string? PmisUnitCode, Guid Id, int? GridTypeId)>> index,
        string? name, string? unitCode, Guid id, int? gridTypeId)
    {
        var key = NormalizeLineName(name);
        if (key == null) return;
        if (!index.TryGetValue(key, out var candidates))
            index[key] = candidates = [];
        candidates.Add((unitCode, id, gridTypeId));
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

    /// <summary>parentGridTypeId: GRIDTYPEID hiện có của đường trục cha (null nếu là gốc, hoặc chưa xác
    /// định được cha) — cho nhánh MƯỢN TẠM khi bản thân nhánh không có capDienAp riêng (PMIS không trả
    /// field này cho phần lớn nhánh, chỉ trục mới luôn có). Không ảnh hưởng tới đường trục gốc thật (luôn
    /// có capDienAp của chính nó) hay nhánh đã tự xác định được cấp điện áp riêng.</summary>
    private static UpsertInfrastructureFromPmisRequest BuildLineUpsertRequest(PmisLineDto item, bool isRoot, Guid? parentId, int? parentGridTypeId) =>
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
            GridTypeId = ResolveGridTypeId(item.CapDienAp) ?? parentGridTypeId,
            IsRootLine = isRoot,
            ParentInfrastructureId = parentId
        };

    /// <summary>Xem IPmisSyncExecutionService.BackfillLineParentsAsync — chạy ĐỘC LẬP từ job Quartz riêng
    /// (LineParentBackfillJob, tick định kỳ), KHÔNG còn chèn vào lượt đồng bộ Đường dây nào. Xử lý 2 loại
    /// candidate từ GetLinesNeedingBackfillAsync: (1) chưa có cha — resolve theo tên qua _lineNameIndex
    /// (như cũ); (2) đã có cha, chỉ thiếu GridTypeId — dùng THẲNG ParentId/ParentGridTypeId đã JOIN sẵn
    /// trong DTO, KHÔNG resolve lại theo tên (tránh bị tính nhầm "chưa xác định được cha" nếu tên trùng/
    /// đổi tên — cha vốn đã biết chắc). Chỉ tải _lineNameIndex khi thật sự có candidate loại (1) — loại (2)
    /// không cần, tránh tải cả danh mục Đường dây vô ích mỗi tick khi không có gì cần resolve theo tên.</summary>
    public async Task<(int WarningDelta, string? ErrorMessage)> BackfillLineParentsAsync()
    {
        List<LineNameIndexEntry> candidates;
        try
        {
            candidates = await _equipmentServiceClient.GetLinesNeedingBackfillAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi khi tải danh sách Đường dây cần khớp lại cha/cấp điện áp, bỏ qua backfill lượt này.");
            return (0, null);
        }

        if (candidates.Count == 0) return (0, null);

        if (candidates.Any(c => c.ParentId == null))
            _lineNameIndex ??= await LoadLineNameIndexAsync();

        var toBackfill = new List<BackfillLineParentItem>();
        var stillUnresolved = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            // An toàn: dừng thu thập thêm nếu đã đạt trần/lượt — UpdateParentIdsAsync (EquipmentService)
            // cập nhật TUẦN TỰ từng dòng (không phải 1 câu SQL gộp), nếu số lượng lên tới hàng nghìn (vd
            // do 1 bug khác khiến hầu hết đường dây tạm thời bị coi là "chưa xác định cha") có thể mất rất
            // lâu — từng khiến SyncHistoryWatchdogJob đánh rớt cả lượt vì treo RUNNING quá 30 phút thật sự
            // trên production. Phần còn lại tự thử tiếp ở lượt sau (candidates vẫn còn PARENT_ID=null nên
            // GetLinesNeedingBackfillAsync lượt sau vẫn thấy), không mất dữ liệu, chỉ trải đều ra nhiều lượt.
            if (toBackfill.Count >= MaxBackfillPerRun)
            {
                // Chỉ đếm phần THẬT SỰ thuộc loại (1, chưa có cha) và có tên hợp lệ vào stillUnresolved —
                // khớp đúng điều kiện continue ở loại (1) bên dưới. Loại (2, đã có cha) không được tính vào
                // đây (dù còn tồn đọng), vì cha đã biết rõ, không phải "mồ côi" — trước đây cộng nhầm CẢ 2
                // loại, làm thổi phồng số liệu cảnh báo khi vượt trần.
                var remaining = candidates.Skip(i).Count(c => c.ParentId == null && ResolveParentLineName(c.Name) != null);
                Log.Warning("PmisSyncExecutionService: backfill cha đường dây đạt trần {Max}/lượt, còn {Remaining} dòng sẽ thử tiếp ở lượt sau.", MaxBackfillPerRun, remaining);
                stillUnresolved += remaining;
                break;
            }

            var candidate = candidates[i];

            if (candidate.ParentId != null)
            {
                // Loại (2): đã có cha — dùng thẳng ParentGridTypeId đã JOIN sẵn (GetLinesNeedingBackfillAsync),
                // không resolve lại theo tên. Cha cũng chưa có GridTypeId thì chưa có gì để mượn — bỏ qua,
                // tự thử lại lượt sau (KHÔNG cộng stillUnresolved, vì cha đã biết rõ, không phải "mồ côi").
                // candidate.GridTypeId chắc chắn NULL ở đây — SQL của GetLinesNeedingBackfillAsync đã đảm
                // bảo GRIDTYPEID IS NULL bất cứ khi nào PARENT_ID IS NOT NULL (2 nhánh OR loại trừ nhau).
                if (candidate.ParentGridTypeId != null)
                {
                    toBackfill.Add(new BackfillLineParentItem
                    {
                        Id = candidate.Id,
                        ParentInfrastructureId = candidate.ParentId.Value,
                        GridTypeId = candidate.ParentGridTypeId
                    });
                }
                continue;
            }

            // Loại (1): chưa có cha. GetLinesNeedingBackfillAsync lọc thô bằng "tên có chứa '/'" (SQL không
            // thể tái hiện chính xác quy tắc ResolveParentLineName — tách theo dấu "/" CUỐI CÙNG) — 1 số
            // tên như "/ABC" (dấu "/" duy nhất nằm ở VỊ TRÍ ĐẦU) qua đúng quy tắc đó lại được coi là GỐC
            // (không có cha), không phải nhánh thật. Bỏ qua hẳn các trường hợp này thay vì tính là "chưa
            // xác định được cha" — PARENT_ID=null với 1 dòng THẬT SỰ là gốc là đúng, không phải lỗi, không
            // nên cảnh báo mãi mãi.
            if (ResolveParentLineName(candidate.Name) == null) continue;

            var (isRoot, parentId, parentGridTypeId) = ResolveParentLineId(candidate.Name, candidate.PmisUnitCode);
            if (!isRoot && parentId is { } resolvedId)
            {
                // Mượn tạm GridTypeId của cha CHỈ khi nhánh này đang thiếu (candidate.GridTypeId từ
                // GetLinesNeedingBackfillAsync) — không đoán đè lên giá trị nhánh đã tự có từ trước.
                toBackfill.Add(new BackfillLineParentItem
                {
                    Id = candidate.Id,
                    ParentInfrastructureId = resolvedId,
                    GridTypeId = candidate.GridTypeId == null ? parentGridTypeId : null
                });
                continue;
            }

            // Không khớp được ĐÚNG cha ở cấp liền kề — nhánh nhiều cấp không cố định mà PMIS không tự
            // cung cấp bản ghi cho waypoint trung gian (vd "A/Nhánh B/Nhánh C/Nhánh D" chỉ có bản ghi PMIS
            // cho lá D). Chỉ mapping theo dữ liệu PMIS thật sự có — KHÔNG tự tạo thêm Đường dây cho các
            // waypoint còn thiếu; giữ nguyên PARENT_ID cũ, tự khớp lại đúng ở lượt sau nếu PMIS bổ sung
            // bản ghi cho waypoint đó.
            stillUnresolved++;
        }

        var resolved = 0;
        if (toBackfill.Count > 0)
        {
            try
            {
                resolved = await _equipmentServiceClient.BackfillLineParentsAsync(toBackfill);
                // updatedCount có thể ÍT HƠN số đã gửi (vd dòng bị xoá/IsDeleted đúng lúc giữa lượt đọc
                // GetLinesNeedingBackfillAsync và lượt UPDATE — UpdateParentIdsAsync lọc IsDeleted=0 nên âm
                // thầm bỏ qua dòng đó) — phần chênh lệch vẫn phải tính là "chưa xác định được cha" thay vì
                // biến mất khỏi mọi con số báo cáo (trước đây chỉ cộng stillUnresolved khi có exception).
                if (resolved < toBackfill.Count) stillUnresolved += toBackfill.Count - resolved;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PmisSyncExecutionService: lỗi khi gửi backfill PARENT_ID cho {Count} đường dây, sẽ tự thử lại ở lượt sau.", toBackfill.Count);
                stillUnresolved += toBackfill.Count;
            }
        }

        if (resolved > 0)
            Log.Information("PmisSyncExecutionService: đã tự khớp lại cha cho {Count} đường dây đã đồng bộ từ trước (trước đó chưa xác định được cha).", resolved);

        var errorMessage = stillUnresolved > 0
            ? $"Đường dây: còn {stillUnresolved} nhánh đã đồng bộ từ trước vẫn chưa xác định được cha."
            : null;

        return (stillUnresolved, errorMessage);
    }

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(
        int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        // Chỉ tải danh mục Đường dây khi thật sự đồng bộ Đường dây, 1 lần/lượt (Scoped: xuyên suốt các
        // trang PMIS trong cùng 1 lần chạy tự động/thủ công) — xem LoadLineNameIndexAsync.
        if (infraTypeId == 2)
            _lineNameIndex ??= await LoadLineNameIndexAsync();

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
                    GridTypeId = ResolveGridTypeId(item.CapDienAp)
                };
            }).ToList();

            results = upsertRequests.Count == 0
                ? []
                : await UpsertInfrastructureSafeAsync(upsertRequests);
        }
        else
        {
            // Đường dây: xử lý TRỤC (không có "/") trước, NHÁNH (có "/") sau — trong CÙNG 1 trang này.
            // Trục vừa lưu thành công được đưa ngay vào _lineNameIndex (AddLineToIndex) để nhánh xử lý
            // NGAY SAU trong cùng trang, hoặc ở trang kế tiếp trong cùng lượt (_lineNameIndex là field
            // tồn tại xuyên suốt cả lượt chạy), tìm thấy cha luôn — không phải đợi lượt chạy sau. Vẫn
            // build lại đúng theo THỨ TỰ GỐC của rawItems ở cuối để details/rawItems[i] bên dưới khớp chỉ số.
            var lineItems = new PmisLineDto[rawItems.Count];
            var rootIndices = new List<int>();
            var branchIndices = new List<int>();
            for (var i = 0; i < rawItems.Count; i++)
            {
                var item = rawItems[i].Deserialize<PmisLineDto>(JsonOptions)!;
                lineItems[i] = item;
                (ResolveParentLineName(item.TenDuongDay) == null ? rootIndices : branchIndices).Add(i);
            }

            var requestsByIndex = new UpsertInfrastructureFromPmisRequest?[rawItems.Count];
            var resultsByIndex = new UpsertInfrastructureFromPmisResult?[rawItems.Count];

            if (rootIndices.Count > 0)
            {
                var rootRequests = rootIndices.Select(i => BuildLineUpsertRequest(lineItems[i], isRoot: true, parentId: null, parentGridTypeId: null)).ToList();
                var rootResults = await UpsertInfrastructureSafeAsync(rootRequests);
                for (var k = 0; k < rootIndices.Count; k++)
                {
                    var idx = rootIndices[k];
                    requestsByIndex[idx] = rootRequests[k];
                    resultsByIndex[idx] = rootResults[k];
                    // CHỈ thêm khi WasCreated=true (trục THẬT SỰ mới, chưa từng có trong _lineNameIndex) —
                    // PMIS trả về TOÀN BỘ dữ liệu mỗi lượt (không phải delta), nên 1 trục ĐÃ tồn tại từ
                    // trước (WasCreated=false, chỉ update) sẽ được xử lý lại ở MỌI lượt sau; nếu vẫn thêm
                    // vào đây sẽ tạo ứng viên TRÙNG với chính nó đã có sẵn từ LoadLineNameIndexAsync đầu
                    // lượt, phá vỡ "candidates.Count == 1" trong ResolveParentLineId cho mọi nhánh của trục
                    // đó — bug thật đã gặp, xem code-review.
                    if (rootResults[k] is { Success: true, WasCreated: true, InfrastructureId: { } newId })
                        AddLineToIndex(lineItems[idx].TenDuongDay, lineItems[idx].MaDonVi, newId, rootRequests[k].GridTypeId);
                }
            }

            if (branchIndices.Count > 0)
            {
                var branchRequests = new List<UpsertInfrastructureFromPmisRequest>(branchIndices.Count);
                foreach (var idx in branchIndices)
                {
                    var item = lineItems[idx];
                    var (isRoot, parentId, parentGridTypeId) = ResolveParentLineId(item.TenDuongDay, item.MaDonVi);
                    branchRequests.Add(BuildLineUpsertRequest(item, isRoot, parentId, parentGridTypeId));
                }

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

        if (upsertRequests.Count == 0) return (0, 0, 0, []);

        var details = new List<SyncHistoryDetail>();
        var errors = new List<string>();
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
                TargetId = result.InfrastructureId?.ToString(),
                ActionType = !result.HasChanged ? SyncActionType.Skip : (result.WasCreated ? SyncActionType.Create : SyncActionType.Update),
                Status = result.Success ? SyncDetailStatus.Success : SyncDetailStatus.Failed,
                DataContent = rawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);

        // Đồng bộ tài liệu đính kèm (API 8/9) cho từng Trạm/Đường dây vừa lưu thành công — lỗi ở bước này
        // CHỈ ghi cảnh báo, không ảnh hưởng successCount/errors ở trên (xem SyncDocumentsForOwnerAsync).
        // KHÔNG đếm số đường dây "/" chưa xác định được cha ở NGAY ĐÂY — việc đó nay thuộc riêng
        // LineParentBackfillJob (job Quartz chạy nền định kỳ, độc lập với lượt sync này, xem
        // BackfillLineParentsAsync) để không đếm trùng — backfill là nơi DUY NHẤT báo số liệu cuối cùng.
        var warnings = 0;
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

        return (successCount, results.Count - successCount, warnings, errors);
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
        foreach (var raw in rawItems)
        {
            var item = raw.Deserialize<EquipmentSaveShape>(JsonOptions)!;

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

        if (upsertRequests.Count == 0) return (0, 0, 0, []);

        var results = await _equipmentServiceClient.UpsertEquipmentAsync(upsertRequests);

        var details = new List<SyncHistoryDetail>();
        var errors = new List<string>();
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
                DataContent = rawItems[i].GetRawText(),
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

        return (successCount, results.Count - successCount, warnings, errors);
    }

    /// <summary>
    /// Đồng bộ tài liệu đính kèm (API 8 SUBSTATION_DOCUMENT_LIST / API 9 LINE_DOCUMENT_LIST) cho 1 Trạm/
    /// Đường dây/Thiết bị đã lưu thành công — tải file thật qua URL PMIS trả về, gửi base64 sang
    /// EquipmentService để lưu MinIO. Lỗi ở BẤT KỲ bước nào (gọi API danh sách, tải file, lưu) đều CHỈ
    /// tạo dòng SyncHistoryDetail trạng thái Warning — không throw ra ngoài, không được cộng vào
    /// successCount/errors của bản ghi chính (Trạm/Đường dây/Thiết bị đã lưu xong trước khi gọi hàm này).
    /// </summary>
    private async Task<(int Warning, List<SyncHistoryDetail> Details)> SyncDocumentsForOwnerAsync(
        string ownerType, string ownerPmisCode, string sourceName, bool isSubstationOrigin,
        string? maTBA, string? maDuongDay, string? maTB, string syncHistoryId)
    {
        var details = new List<SyncHistoryDetail>();
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
    /// Tách tên đường dây CHA từ tên đầy đủ, theo đúng quy ước đặt tên PMIS đang dùng thật:
    /// "&lt;đường trục&gt;/Nhánh A/Nhánh B/Nhánh C" — mỗi cấp phân cách bởi ký tự "/", cha của 1 dòng là
    /// phần tên đứng trước dấu "/" CUỐI CÙNG (không phải dấu "/" đầu tiên, để xử lý đúng nhiều cấp lồng
    /// nhau: cha của "A/B/C" là "A/B", không phải "A"). Không có "/" → null (đường trục gốc, không có cha).
    /// </summary>
    internal static string? ResolveParentLineName(string? tenDuongDay)
    {
        if (string.IsNullOrWhiteSpace(tenDuongDay)) return null;
        var lastSlash = tenDuongDay.LastIndexOf('/');
        if (lastSlash <= 0) return null;
        var parentName = tenDuongDay[..lastSlash].Trim();
        return parentName.Length > 0 ? parentName : null;
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
