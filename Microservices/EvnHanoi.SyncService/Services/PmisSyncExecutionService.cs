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

    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;

    public PmisSyncExecutionService(
        IEquipmentServiceClient equipmentServiceClient, ISyncHistoryRepository syncHistoryRepository, IPmisClient pmisClient)
    {
        _equipmentServiceClient = equipmentServiceClient;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
    }

    public async Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(
        int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
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
                upsertRequests.Add(new UpsertInfrastructureFromPmisRequest
                {
                    InfraTypeId = 2,
                    PmisCode = item.MaDuongDay,
                    Code = item.MaDuongDay,
                    Name = item.TenDuongDay,
                    UnitCode = item.MaDonVi,
                    OperationDate = item.NgayVanHanh,
                    GridTypeId = ResolveGridTypeId(item.CapDienAp)
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
                ActionType = result.WasCreated ? SyncActionType.Create : SyncActionType.Update,
                Status = result.Success ? SyncDetailStatus.Success : SyncDetailStatus.Failed,
                DataContent = rawItems[i].GetRawText(),
                ErrorMessage = result.ErrorMessage
            });
        }

        await _syncHistoryRepository.InsertDetailsAsync(details);

        // Đồng bộ tài liệu đính kèm (API 8/9) cho từng Trạm/Đường dây vừa lưu thành công — lỗi ở bước này
        // CHỈ ghi cảnh báo, không ảnh hưởng successCount/errors ở trên (xem SyncDocumentsForOwnerAsync).
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

            upsertRequests.Add(new UpsertEquipmentFromPmisRequest
            {
                PmisCode = maTB,
                Code = maTB,
                Name = tenTB,
                EquipmentTypeCode = item.MaLoaiTB ?? string.Empty,
                EquipmentTypeName = item.TenLoaiTB,
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
                ActionType = result.WasCreated ? SyncActionType.Create : SyncActionType.Update,
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
            List<(string MaTaiLieu, string? TenTaiLieu, string? LoaiTaiLieu, string? File)> items;
            if (isSubstationOrigin)
            {
                var resp = await _pmisClient.GetSubstationDocumentsAsync(new PmisSubstationDocumentSearchRequest
                {
                    MaTBA = maTBA,
                    MaTB = maTB,
                    Take = 200
                });
                items = resp.Items.Select(d => (d.MaTaiLieu, d.TenTaiLieu, d.LoaiTaiLieu, d.File)).ToList();
            }
            else
            {
                var resp = await _pmisClient.GetLineDocumentsAsync(new PmisLineDocumentSearchRequest
                {
                    MaDuongDay = maDuongDay,
                    MaTB = maTB,
                    Take = 200
                });
                items = resp.Items.Select(d => (d.MaTaiLieu, d.TenTaiLieu, d.LoaiTaiLieu, d.File)).ToList();
            }

            if (items.Count == 0) return (0, details);

            var requests = new List<UpsertPmisDocumentRequest>();
            foreach (var doc in items)
            {
                if (string.IsNullOrWhiteSpace(doc.MaTaiLieu)) continue;

                string? fileBase64 = null;
                if (!string.IsNullOrWhiteSpace(doc.File))
                {
                    var bytes = await _pmisClient.DownloadDocumentFileAsync(doc.File);
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
                    FileBase64 = fileBase64
                });
            }

            if (requests.Count == 0) return (0, details);

            var results = await _equipmentServiceClient.UpsertDocumentsAsync(requests);
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
