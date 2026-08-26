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

    public async Task<(int Success, int Failed, List<string> Errors)> SyncInfrastructureAsync(
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

        if (upsertRequests.Count == 0) return (0, 0, []);

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
        return (successCount, results.Count - successCount, errors);
    }

    public async Task<(int Success, int Failed, List<string> Errors)> SyncEquipmentAsync(
        string syncHistoryId, IReadOnlyList<JsonElement> rawItems)
    {
        var upsertRequests = new List<UpsertEquipmentFromPmisRequest>();
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
                ParentPmisCode = item.MaTBA ?? item.MaDuongDay,
                UnitCode = item.MaDonVi,
                ManufactureYear = item.NamSanXuat,
                QrCodeBase64 = qrCodeBase64,
                GridTypeId = gridTypeId,
                ThongSoKyThuat = thongSoKyThuat
            });
        }

        if (upsertRequests.Count == 0) return (0, 0, []);

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
        return (successCount, results.Count - successCount, errors);
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
        public string? MaTBA { get; set; }
        public string? MaDuongDay { get; set; }
        public string? MaDonVi { get; set; }
        public int? NamSanXuat { get; set; }
        public string? MaQRCode { get; set; }
        public string? ThongSoKyThuat { get; set; }
    }
}
