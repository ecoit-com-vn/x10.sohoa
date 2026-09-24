using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Job đối chiếu NHẸ, chạy 1 lần/ngày, KHÔNG tự sửa gì — chỉ log cảnh báo cho admin tự quyết định có cần
/// chạy lại đồng bộ thủ công hay không. 2 việc độc lập gộp vào 1 job vì cùng tần suất/mục đích "lưới an
/// toàn cho những rủi ro đã biết nhưng không kiểm soát được tận gốc" (audit PMIS 2026-09-24, Giai đoạn 4):
///
/// 1. Đối chiếu phân trang PMIS (Substation/Line): PMIS trả danh sách qua Skip/Take KHÔNG có sort-key ổn
///    định tường minh (xem PmisSubstationSearchRequest/PmisLineSearchRequest) — nếu thứ tự PMIS trả về
///    thay đổi giữa 2 lần gọi (thêm/xoá bản ghi phía trước vị trí đang phân trang), 1 lượt đồng bộ resume
///    dở dang (chạm SyncConfig.SyncCursor) có thể bỏ sót bản ghi đã dịch chuyển — rủi ro NGOÀI khả năng
///    kiểm soát từ phía SyncService (phụ thuộc hành vi PMIS). Job này CHỈ lấy danh sách MÃ (không chi tiết)
///    của TOÀN BỘ PMIS rồi so với mã ĐÃ có trong DB (qua GetSyncedInfrastructurePmisCodesAsync — cùng
///    nguồn dữ liệu PmisScheduledSyncJob.RunEquipmentAsync đã dùng để xoay vòng) — log số mã PMIS có mà DB
///    chưa có, admin tự đánh giá bất thường nếu số này lớn/tăng dần.
///
/// 2. Compensating check cho EquipmentTbaTransferredEvent có thể mất: InternalPmisSyncController ghi DB
///    "chuyển TBA" và publish event RabbitMQ là 2 BƯỚC RIÊNG (không transaction) — nếu publish lỗi ngay
///    sau khi DB đã ghi (catch log Error, không throw), sự kiện chuyển TBA có thể không tới được
///    NotificationService dù dữ liệu đã đúng. Không đủ hạ tầng (outbox) để tự phát hiện CHÍNH XÁC sự kiện
///    nào bị mất, nên CHỈ log tổng số thiết bị "Đã chuyển TBA" trong 24h gần nhất để admin đối chiếu thủ
///    công với số thông báo NotificationService thực nhận — xem
///    EquipmentRepository.CountRecentlyTransferredAsync.
/// </summary>
public class PmisReconciliationJob : IJob
{
    private const int PageSize = 2000;

    private readonly IPmisClient _pmisClient;
    private readonly IEquipmentServiceClient _equipmentServiceClient;

    public PmisReconciliationJob(IPmisClient pmisClient, IEquipmentServiceClient equipmentServiceClient)
    {
        _pmisClient = pmisClient;
        _equipmentServiceClient = equipmentServiceClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var syncedCodes = (await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync())
                .Select(c => c.PmisCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await ReconcileAsync("Trạm biến áp", syncedCodes, async skip =>
            {
                var result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest { Skip = skip, Take = PageSize });
                return (result.Items.Select(i => i.MaTBA).ToList(), result.Total);
            });

            await ReconcileAsync("Đường dây", syncedCodes, async skip =>
            {
                var result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest { Skip = skip, Take = PageSize });
                return (result.Items.Select(i => i.MaDuongDay).ToList(), result.Total);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisReconciliationJob: lỗi khi đối chiếu danh sách mã PMIS — bỏ qua lượt này, thử lại ngày mai.");
        }

        try
        {
            var count = await _equipmentServiceClient.GetRecentlyTransferredCountAsync(24);
            if (count > 0)
                Log.Information("PmisReconciliationJob: {Count} thiết bị được đánh dấu \"Đã chuyển TBA\" trong 24h qua — đối chiếu thủ công với số thông báo NotificationService thực nhận nếu nghi ngờ có sự kiện bị thiếu (publish RabbitMQ lỗi ngay sau khi DB ghi thành công).", count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisReconciliationJob: lỗi khi đếm thiết bị chuyển TBA gần đây — bỏ qua lượt này, thử lại ngày mai.");
        }
    }

    private static async Task ReconcileAsync(
        string label, HashSet<string> syncedCodes, Func<int, Task<(List<string?> Codes, int Total)>> fetchPage)
    {
        var pmisCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skip = 0;
        while (true)
        {
            var (codes, total) = await fetchPage(skip);
            foreach (var code in codes)
            {
                if (!string.IsNullOrWhiteSpace(code)) pmisCodes.Add(code.Trim());
            }
            if (codes.Count < PageSize || skip + codes.Count >= total) break;
            skip += PageSize;
        }

        var missingFromDb = pmisCodes.Count(code => !syncedCodes.Contains(code));
        if (missingFromDb > 0)
        {
            Log.Warning("PmisReconciliationJob: {Label} — {Missing}/{Total} mã PMIS CHƯA có trong DB (có thể do phân trang lệch giữa 2 lượt đồng bộ, hoặc đơn giản là chưa tới lượt đồng bộ) — kiểm tra nếu số này lớn/tăng dần qua các ngày.", label, missingFromDb, pmisCodes.Count);
        }
        else
        {
            Log.Information("PmisReconciliationJob: {Label} — {Total} mã PMIS đều đã có trong DB, không phát hiện lệch.", label, pmisCodes.Count);
        }
    }
}
