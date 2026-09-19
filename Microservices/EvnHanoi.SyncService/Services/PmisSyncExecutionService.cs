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
    private const int DocumentMaxPages = 50; // an toàn: tối đa 50.000 tài liệu/đối tượng/lần đồng bộ
    private const int DocumentUpsertBatchSize = 20; // gửi theo lô, tránh 1 request base64 hoá hết cả nghìn tài liệu

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

    // Danh mục Đường dây hiện có (tên đã chuẩn hoá -> danh sách ứng viên [mã đơn vị PMIS, Id]) — tải 1
    // lần/lượt đồng bộ Đường dây, KHÔNG tải lại theo từng dòng — dùng để tự tìm cha theo tên trong bộ nhớ
    // thay vì mỗi dòng tự query DB riêng (xem ResolveParentLineIdAsync). Khoá bằng tên đã chuẩn hoá
    // (NormalizeLineName) để không nhạy khoảng trắng thừa/khoảng trắng kép giữa dòng trục và dòng nhánh.
    private Dictionary<string, List<(string? PmisUnitCode, Guid Id)>>? _lineNameIndex;

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
    private async Task<Dictionary<string, List<(string? PmisUnitCode, Guid Id)>>> LoadLineNameIndexAsync()
    {
        try
        {
            var entries = await _equipmentServiceClient.GetLineNameIndexAsync();
            var index = new Dictionary<string, List<(string?, Guid)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var key = NormalizeLineName(entry.Name);
                if (key == null) continue;
                if (!index.TryGetValue(key, out var candidates))
                    index[key] = candidates = [];
                candidates.Add((entry.PmisUnitCode, entry.Id));
            }
            return index;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisSyncExecutionService: lỗi khi tải danh mục Đường dây hiện có, bỏ qua việc gán cha-con đường dây cho cả lượt chạy này.");
            return new Dictionary<string, List<(string?, Guid)>>(StringComparer.OrdinalIgnoreCase);
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
    /// lần/lượt đồng bộ (KHÔNG tự query DB — xem LoadLineNameIndexAsync). Trả về (IsRoot, ParentId):
    /// - Tên không có "/" → (true, null) — chắc chắn là đường trục gốc, không có cha.
    /// - Có "/" nhưng danh mục không có tên trục đó (trục chưa đồng bộ tới trong lượt này, hoặc lệch tên
    ///   dù đã chuẩn hoá) → (false, null) — caller giữ nguyên PARENT_ID cũ, không xoá.
    /// - Có "/" và khớp đúng 1 ứng viên (theo tên, hoặc theo tên + mã đơn vị nếu trùng tên nhiều nơi) →
    ///   (false, Id đó).
    /// - Có "/" nhưng trùng tên ở ≥2 nơi KHÔNG phân biệt được bằng mã đơn vị (dữ liệu PMIS thật sự có tên
    ///   trục trùng nhau) → (false, null) + cảnh báo 1 lần/tên trục, không đoán đại 1 trong số đó.
    /// </summary>
    private (bool IsRoot, Guid? ParentId) ResolveParentLineId(string? tenDuongDay, string? maDonVi)
    {
        var parentNameRaw = ResolveParentLineName(tenDuongDay);
        if (parentNameRaw == null) return (true, null); // không có "/" -> chắc chắn là gốc

        var normalized = NormalizeLineName(parentNameRaw);
        if (normalized == null || _lineNameIndex == null || !_lineNameIndex.TryGetValue(normalized, out var candidates))
            return (false, null); // trục chưa có trong danh mục lượt này — giữ nguyên PARENT_ID cũ, tự khớp đúng ở lượt sau

        if (candidates.Count == 1) return (false, candidates[0].Id);

        // Trùng tên ở nhiều nơi — thử phân biệt bằng mã đơn vị PMIS của chính dòng đang xử lý.
        var sameUnit = candidates.Where(c => string.Equals(c.PmisUnitCode, maDonVi, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameUnit.Count == 1) return (false, sameUnit[0].Id);

        if (_warnedAmbiguousParentNames.Add(normalized))
        {
            Log.Warning("PmisSyncExecutionService: tên đường trục '{ParentName}' trùng ở {Count} đường dây khác nhau, không phân biệt được bằng mã đơn vị — bỏ qua gán cha cho các nhánh tham chiếu tới tên này.",
                normalized, candidates.Count);
        }
        return (false, null);
    }

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(
        int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        // Chỉ tải danh mục Đường dây khi thật sự đồng bộ Đường dây, 1 lần/lượt (Scoped: xuyên suốt các
        // trang PMIS trong cùng 1 lần chạy tự động/thủ công) — xem LoadLineNameIndexAsync.
        if (infraTypeId == 2)
            _lineNameIndex ??= await LoadLineNameIndexAsync();

        var unresolvedParentRefCount = 0;
        var upsertRequests = new List<UpsertInfrastructureFromPmisRequest>();
        foreach (var raw in rawItems)
        {
            if (infraTypeId == 1)
            {
                var item = raw.Deserialize<PmisSubstationDto>(JsonOptions)!;
                upsertRequests.Add(new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 1,
                    PmisCode = item.MaTBA,
                    Code = item.MaTBA,
                    Name = item.TenTBA,
                    Address = item.DiaDiem,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh,
                    GridTypeId = ResolveGridTypeId(item.CapDienAp)
                });
            }
            else
            {
                var item = raw.Deserialize<PmisLineDto>(JsonOptions)!;
                var (isRoot, parentId) = ResolveParentLineId(item.TenDuongDay, item.MaDonVi);
                if (!isRoot && parentId == null) unresolvedParentRefCount++;

                upsertRequests.Add(new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 2,
                    PmisCode = item.MaDuongDay,
                    Code = item.MaDuongDay,
                    Name = item.TenDuongDay,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh,
                    GridTypeId = ResolveGridTypeId(item.CapDienAp),
                    IsRootLine = isRoot,
                    ParentInfrastructureId = parentId
                });
            }
        }

        if (upsertRequests.Count == 0) return (0, 0, 0, []);

        var results = await _equipmentServiceClient.UpsertInfrastructureAsync(upsertRequests);

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
        // Cộng thêm số đường dây có "/" nhưng chưa xác định được cha (xem ResolveParentLineId) — để lộ ra
        // qua Lịch sử đồng bộ thay vì âm thầm, dù đây thường chỉ là tình huống tạm thời tự hết sau 1-2 lượt.
        var warnings = unresolvedParentRefCount;
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

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(
        string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        var upsertRequests = new List<UpsertEquipmentFromPmisRequest>();
        // Song song 1:1 với upsertRequests — giữ lại ngữ cảnh gốc (TBA hay đường dây, mã cha) để đồng bộ
        // tài liệu đính kèm (API 8/9) đúng đối tượng sau khi thiết bị đã lưu thành công.
        var origins = new List<(bool IsSubstationOrigin, string? MaTBA, string? MaDuongDay, string MaTB)>();
        foreach (var raw in rawItems)
        {
            var item = raw.Deserialize<EquipmentSaveShape>(JsonOptions)!;

            // Thiết bị TBA (nhận diện bằng MaThietBi có giá trị — chỉ dạng thiết bị này mới có field
            // này, xem PmisSubstationDeviceDto) không có sẵn ThongSoKyThuat/MaQRCode trong danh sách,
            // khác thiết bị đường dây (đã có sẵn) — phải gọi thêm ChiTietThietBi (API 7) ngay tại đây,
            // tự động trong lúc đồng bộ, không chờ người dùng bấm gì thêm.
            var isSubstationDevice = !string.IsNullOrWhiteSpace(item.MaThietBi);
            var maTB = isSubstationDevice ? item.MaThietBi! : item.MaTB;
            var tenTB = isSubstationDevice ? item.TenThietBi! : item.TenTB;
            var thongSoKyThuat = item.ThongSoKyThuat;
            var maQRCode = item.MaQRCode;

            if (isSubstationDevice)
            {
                try
                {
                    var detail = await _pmisClient.GetDeviceDetailAsync(new PmisDeviceDetailRequest
                    {
                        MaThietBi = maTB,
                        MaTBA = item.MaTBA
                    });
                    thongSoKyThuat = detail?.ThongSoKyThuat;
                    maQRCode = detail?.MaQRCode;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "PmisSyncExecutionService: lỗi gọi ChiTietThietBi cho thiết bị TBA {MaThietBi}, bỏ qua thông số kỹ thuật/QR.", maTB);
                }
            }

            // maQRCode PMIS trả về là URL (vd. ".../AnhQRCode?idPmis=..."), không phải base64 — phải tải
            // ảnh nhị phân thật rồi tự encode base64 mới lưu đúng vào EQUIPMENTS.QR_CODE (giữ nguyên
            // field/cột cũ, chỉ sửa cách lấy giá trị).
            string? qrCodeBase64 = null;
            if (!string.IsNullOrWhiteSpace(maQRCode))
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

            upsertRequests.Add(new UpsertEquipmentFromPmisRequest
            {
                PmisCode = maTB,
                Code = maTB,
                Name = tenTB,
                EquipmentTypeCode = item.MaLoaiTB ?? string.Empty,
                EquipmentTypeName = equipmentTypeName,
                ParentPmisCode = item.MaTBA ?? item.MaDuongDay,
                UnitCode = item.MaDonVi,
                ManufactureYear = item.NamSanXuat,
                QrCodeBase64 = qrCodeBase64,
                GridTypeId = gridTypeId,
                ThongSoKyThuat = thongSoKyThuat
            });
            origins.Add((isSubstationDevice, item.MaTBA, item.MaDuongDay, maTB));
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
            for (var page = 0; page < DocumentMaxPages; page++)
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
            }

            if (items.Count == 0) return (0, details);

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
            foreach (var doc in items)
            {
                if (string.IsNullOrWhiteSpace(doc.MaTaiLieu)) continue;

                string? fileBase64 = null;
                if (!string.IsNullOrWhiteSpace(doc.File))
                {
                    var bytes = await _pmisClient.DownloadDocumentFileAsync(doc.File, endpointApiCode);
                    if (bytes is { Length: > 0 }) fileBase64 = Convert.ToBase64String(bytes);
                }

                requests.Add(new UpsertPmisDocumentRequest
                {
                    PmisDocumentCode = doc.MaTaiLieu,
                    OwnerType = ownerType,
                    OwnerPmisCode = ownerPmisCode,
                    DocumentName = doc.TenTaiLieu,
                    DocumentType = doc.LoaiTaiLieu,
                    FileName = doc.TenTaiLieu ?? doc.MaTaiLieu,
                    FileBase64 = fileBase64,
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

            if (results.Count == 0) return (0, details);

            var warningCount = 0;
            foreach (var result in results)
            {
                var isWarning = !result.Success;
                if (isWarning) warningCount++;

                details.Add(new SyncHistoryDetail
                {
                    SyncHistoryId = syncHistoryId,
                    SourceId = result.PmisDocumentCode,
                    SourceCode = ownerPmisCode,
                    SourceName = sourceName,
                    ActionType = SyncActionType.Skip,
                    Status = isWarning ? SyncDetailStatus.Warning : SyncDetailStatus.Success,
                    ErrorMessage = result.ErrorMessage
                });
            }
            return (warningCount, details);
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
    /// MaQRCode/ThongSoKyThuat; thiết bị đường dây dùng MaTB/TenTB, có sẵn cả 2. Khai báo đủ field của
    /// cả 2 phía (đều optional) rồi tự chọn nhánh đúng trong SyncEquipmentAsync theo MaThietBi có giá trị.
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
    }
}
