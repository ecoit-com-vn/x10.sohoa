using System.Security.Claims;
using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace EvnHanoi.SyncService.Controllers;

/// <summary>
/// Đồng bộ thủ công (module 3): Bước 1 tìm kiếm (search, không lưu DB) — Bước 2 người dùng chọn
/// bằng checkbox trên FE — Bước 3 Lưu (save) đúng những bản ghi FE đã gửi lại (không fetch lại PMIS).
/// Logic upsert dùng chung với đồng bộ tự động qua <see cref="IPmisSyncExecutionService"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/sync/manual")]
public class PmisManualSyncController : ControllerBase
{
    private readonly IPmisClient _pmisClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;

    public PmisManualSyncController(
        IInteractivePmisClient pmisClient,
        IPmisSyncExecutionService executionService,
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository)
    {
        _pmisClient = pmisClient;
        _executionService = executionService;
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
    }

    [HttpPost("{objectType}/search")]
    public async Task<IActionResult> Search(string objectType, [FromBody] PmisManualSearchRequest request)
    {
        var normalizedType = objectType.ToUpperInvariant();
        if (!SyncObjectType.IsValid(normalizedType))
            return BadRequest(new { message = "Đối tượng đồng bộ không hợp lệ." });

        try
        {
            var response = normalizedType switch
            {
                SyncObjectType.Substation => await SearchSubstationsAsync(request),
                SyncObjectType.TransmissionLine => await SearchLinesAsync(request),
                SyncObjectType.Equipment => await SearchEquipmentsAsync(request),
                _ => throw new InvalidOperationException()
            };
            return Ok(response);
        }
        catch (PmisEndpointNotConfiguredException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex) when (PmisUpstreamFailure.Matches(ex))
        {
            return StatusCode(503, new { message = PmisUpstreamFailure.UserMessage(ex) });
        }
    }

    [HttpPost("{objectType}/save")]
    public async Task<IActionResult> Save(string objectType, [FromBody] PmisManualSaveRequest request)
    {
        var normalizedType = objectType.ToUpperInvariant();
        if (!SyncObjectType.IsValid(normalizedType))
            return BadRequest(new { message = "Đối tượng đồng bộ không hợp lệ." });
        if (request.Items.Count == 0)
            return BadRequest(new { message = "Chưa chọn bản ghi nào để đồng bộ." });

        string historyId;
        try
        {
            var syncConfig = await _syncConfigRepository.GetByObjectTypeAsync(normalizedType);
            historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
            {
                SyncConfigId = syncConfig?.Id ?? string.Empty,
                ObjectType = normalizedType,
                SyncType = SyncType.Manual,
                StartTime = DateTime.UtcNow,
                Status = SyncHistoryStatus.Running,
                CreatedBy = CurrentUserName()
            });
        }
        catch (Exception ex)
        {
            // Lỗi ngay khi khởi tạo lịch sử đồng bộ (DB SyncService) — chưa gọi gì tới EquipmentService/PMIS.
            Log.Error(ex, "PmisManualSyncController.Save: lỗi khởi tạo lịch sử đồng bộ cho {ObjectType}.", normalizedType);
            return StatusCode(500, new { message = "Không thể khởi tạo lịch sử đồng bộ. Vui lòng thử lại sau." });
        }

        int successCount, failedCount, warningCount;
        List<string> errors;
        try
        {
            (successCount, failedCount, warningCount, errors) = normalizedType switch
            {
                SyncObjectType.Substation => await _executionService.SyncInfrastructureAsync(1, historyId, request.Items),
                SyncObjectType.TransmissionLine => await _executionService.SyncInfrastructureAsync(2, historyId, request.Items),
                SyncObjectType.Equipment => await _executionService.SyncEquipmentAsync(historyId, request.Items),
                _ => throw new InvalidOperationException()
            };
        }
        catch (Exception ex)
        {
            // Không để lỗi bay thẳng thành 500 vô danh — trước đây action này không bắt gì cả nên mọi lỗi
            // gọi sang EquipmentService (token nội bộ sai cấu hình, EquipmentService sập, lỗi DB không
            // nằm trong vòng try/catch theo từng bản ghi ở InternalPmisSyncController, vd. ghi
            // SYNC_HISTORY_DETAIL) đều thành "Lỗi máy chủ nội bộ" và KHÔNG được log ở đâu cả (SyncService
          // không có UseExceptionHandler/UseSerilogRequestLogging toàn cục). Log lại đây để tra được
            // trong Elasticsearch (index app_logs-*) hoặc Logs/log-*.txt, đồng thời đánh dấu lịch sử đồng
            // bộ là Failed thay vì để mãi ở trạng thái Running.
            Log.Error(ex, "PmisManualSyncController.Save: lỗi khi lưu dữ liệu {ObjectType}, syncHistoryId={SyncHistoryId}.", normalizedType, historyId);

            try
            {
                await _syncHistoryRepository.CompleteAsync(
                    historyId, SyncHistoryStatus.Failed, request.Items.Count, 0, request.Items.Count,
                    SyncErrorFormatter.Format(ex));
            }
            catch (Exception completeEx)
            {
                Log.Error(completeEx, "PmisManualSyncController.Save: lỗi khi cập nhật trạng thái Failed cho syncHistoryId={SyncHistoryId}.", historyId);
            }

            var message = ex is HttpRequestException or TimeoutException or TaskCanceledException
                ? "Không lưu được dữ liệu — dịch vụ EquipmentService đang gặp sự cố hoặc không phản hồi. Vui lòng thử lại sau hoặc liên hệ quản trị hệ thống."
                : "Không lưu được dữ liệu do lỗi hệ thống. Vui lòng thử lại sau hoặc liên hệ quản trị hệ thống.";
            return StatusCode(500, new { message });
        }

        var finalStatus = successCount == 0
            ? SyncHistoryStatus.Failed
            : (warningCount > 0 ? SyncHistoryStatus.Warning : SyncHistoryStatus.Success);
        await _syncHistoryRepository.CompleteAsync(
            historyId, finalStatus, request.Items.Count, successCount, failedCount,
            errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);

        return Ok(new PmisManualSaveResponse
        {
            SyncHistoryId = historyId,
            Total = request.Items.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        });
    }

    /// <summary>
    /// Nút "Cập nhật từ PMIS" trên trang chi tiết 1 thiết bị — gọi ChiTietThietBi (API 7) lấy dữ liệu
    /// tươi cho ĐÚNG thiết bị này (dùng IInteractivePmisClient vì người dùng đang chờ phản hồi trực
    /// tiếp, khác client nền dùng cho job đồng bộ tự động), rồi tái dùng NGUYÊN pipeline upsert đã có
    /// (SyncEquipmentAsync) — thiết bị sẽ tự đi qua đúng luồng: upsert → phát hiện đổi trạm nếu có →
    /// cập nhật EQUIPMENT_PMIS_SPEC → tạo form tự động nếu thiếu → mặc định hoá thông số nếu trống.
    /// Dựng shape KHÁC NHAU theo request.IsSubstationDevice — cờ này KHÔNG chỉ quyết định
    /// SyncEquipmentAsync có tự gọi lại ChiTietThietBi hay không, mà còn quyết định đồng bộ tài liệu
    /// đính kèm đi đúng API TBA hay đường dây (SUBSTATION_DOCUMENT_LIST/LINE_DOCUMENT_LIST) — set sai sẽ
    /// làm tài liệu đính kèm đồng bộ nhầm API và luôn thất bại. Thiết bị TBA: set MaThietBi (buộc
    /// SyncEquipmentAsync tự gọi lại ChiTietThietBi 1 lần nữa — chấp nhận gọi PMIS 2 lần vì đây là thao
    /// tác thủ công, ít khi xảy ra, đổi lại route tài liệu đính kèm đúng). Thiết bị đường dây: KHÔNG set
    /// MaThietBi (đã có sẵn đủ dữ liệu từ lần gọi ChiTietThietBi ở trên, khỏi gọi PMIS lần 2).
    /// </summary>
    [HttpPost("equipment/refresh-device")]
    public async Task<IActionResult> RefreshEquipment([FromBody] PmisRefreshEquipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PmisCode))
            return BadRequest(new { message = "Thiết bị chưa được đồng bộ từ PMIS (thiếu mã PMIS) nên không thể cập nhật." });

        PmisDeviceDetailDto? detail;
        try
        {
            detail = await _pmisClient.GetDeviceDetailAsync(new PmisDeviceDetailRequest
            {
                MaThietBi = request.PmisCode,
                MaTBA = request.ParentPmisCode
            });
        }
        catch (PmisEndpointNotConfiguredException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex) when (PmisUpstreamFailure.Matches(ex))
        {
            return StatusCode(503, new { message = PmisUpstreamFailure.UserMessage(ex) });
        }

        if (detail == null)
            return NotFound(new { message = "Không tìm thấy dữ liệu thiết bị này trên PMIS." });

        // TBA: set MaThietBi (giống hệt shape 1 dòng trong DanhSachThietBi TBA) — SyncEquipmentAsync coi
        // đây là "thiết bị TBA", tự gọi lại ChiTietThietBi lấy ThongSoKyThuat/MaQRCode fresh và route tài
        // liệu đính kèm sang SUBSTATION_DOCUMENT_LIST. Đường dây: KHÔNG set MaThietBi, tự điền sẵn
        // ThongSoKyThuat/MaQRCode từ lần gọi ChiTietThietBi ở trên (giống shape DanhSachThietBiDuongDay) —
        // SyncEquipmentAsync route tài liệu đính kèm sang LINE_DOCUMENT_LIST, không gọi lại PMIS lần 2.
        var rawItem = request.IsSubstationDevice
            ? JsonSerializer.SerializeToElement(new
            {
                MaThietBi = detail.MaTB,
                TenThietBi = detail.TenTB,
                MaLoaiTB = detail.MaLoaiTB,
                TenLoaiTB = detail.TenLoaiTB,
                MaTBA = detail.MaTBA,
                MaDonVi = detail.MaDonVi,
                NamSanXuat = detail.NamSanXuat
            })
            : JsonSerializer.SerializeToElement(new
            {
                MaTB = detail.MaTB,
                TenTB = detail.TenTB,
                MaLoaiTB = detail.MaLoaiTB,
                TenLoaiTB = detail.TenLoaiTB,
                MaDuongDay = detail.MaTBA,
                MaDonVi = detail.MaDonVi,
                NamSanXuat = detail.NamSanXuat,
                MaQRCode = detail.MaQRCode,
                ThongSoKyThuat = detail.ThongSoKyThuat
            });

        string historyId;
        try
        {
            var syncConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.Equipment);
            historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
            {
                SyncConfigId = syncConfig?.Id ?? string.Empty,
                ObjectType = SyncObjectType.Equipment,
                SyncType = SyncType.Manual,
                StartTime = DateTime.UtcNow,
                Status = SyncHistoryStatus.Running,
                CreatedBy = (CurrentUserName() ?? "system") + " (cập nhật 1 thiết bị)"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisManualSyncController.RefreshEquipment: lỗi khởi tạo lịch sử đồng bộ cho thiết bị {PmisCode}.", request.PmisCode);
            return StatusCode(500, new { message = "Không thể khởi tạo lịch sử đồng bộ. Vui lòng thử lại sau." });
        }

        int successCount, failedCount, warningCount;
        List<string> errors;
        try
        {
            (successCount, failedCount, warningCount, errors) = await _executionService.SyncEquipmentAsync(historyId, [rawItem]);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisManualSyncController.RefreshEquipment: lỗi khi cập nhật thiết bị {PmisCode}, syncHistoryId={SyncHistoryId}.", request.PmisCode, historyId);
            try
            {
                await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Failed, 1, 0, 1, SyncErrorFormatter.Format(ex));
            }
            catch (Exception completeEx)
            {
                Log.Error(completeEx, "PmisManualSyncController.RefreshEquipment: lỗi khi cập nhật trạng thái Failed cho syncHistoryId={SyncHistoryId}.", historyId);
            }
            return StatusCode(500, new { message = "Không cập nhật được thông số thiết bị do lỗi hệ thống. Vui lòng thử lại sau." });
        }

        var finalStatus = successCount == 0 ? SyncHistoryStatus.Failed : SyncHistoryStatus.Success;
        var errorMessage = errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null;
        await _syncHistoryRepository.CompleteAsync(historyId, finalStatus, 1, successCount, failedCount, errorMessage);

        // TargetId của dòng chi tiết vừa ghi chính là Id thiết bị SAU khi cập nhật — có thể khác Id thiết
        // bị FE đang xem nếu PMIS báo đổi trạm/đường dây (hệ thống tự tạo bản ghi mới, xem
        // UpsertFromPmisAsync) — FE bắt buộc phải điều hướng theo Id này, không được giữ nguyên Id cũ.
        string? resultEquipmentId = null;
        if (successCount > 0)
        {
            try
            {
                var (detailItems, _) = await _syncHistoryRepository.GetDetailsPagedAsync(historyId, 1, 1);
                resultEquipmentId = detailItems.FirstOrDefault()?.TargetId;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PmisManualSyncController.RefreshEquipment: lỗi khi đọc lại Id thiết bị sau cập nhật, syncHistoryId={SyncHistoryId}.", historyId);
            }
        }

        return Ok(new PmisRefreshEquipmentResponse
        {
            Success = successCount > 0,
            Message = successCount > 0 ? "Đã cập nhật thông số thiết bị từ PMIS." : (errorMessage ?? "Không thể cập nhật thông số thiết bị từ PMIS."),
            SyncHistoryId = historyId,
            EquipmentId = resultEquipmentId
        });
    }

    private async Task<PmisManualSearchResponse> SearchSubstationsAsync(PmisManualSearchRequest r)
    {
        var result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest
        {
            MaDonVi = r.MaDonVi,
            LoaiTBA = r.LoaiTBA,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });

        return new PmisManualSearchResponse
        {
            Total = result.Total,
            Items = result.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaTBA,
                DisplayName = item.TenTBA,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private async Task<PmisManualSearchResponse> SearchLinesAsync(PmisManualSearchRequest r)
    {
        var result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest
        {
            MaDonVi = r.MaDonVi,
            MaLoaiDuongDay = r.MaLoaiDuongDay,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });

        return new PmisManualSearchResponse
        {
            Total = result.Total,
            Items = result.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaDuongDay,
                DisplayName = item.TenDuongDay,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private async Task<PmisManualSearchResponse> SearchEquipmentsAsync(PmisManualSearchRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.MaTBA) && string.IsNullOrWhiteSpace(r.MaDuongDay))
            throw new InvalidOperationException("Cần chọn Trạm biến áp (maTBA) hoặc Đường dây (maDuongDay) để tìm thiết bị.");

        if (!string.IsNullOrWhiteSpace(r.MaTBA))
        {
            var result = await _pmisClient.GetSubstationDevicesAsync(new PmisSubstationDeviceSearchRequest
            {
                MaTBA = r.MaTBA,
                MaLoaiTB = r.MaLoaiTB,
                MaDonVi = r.MaDonVi,
                NamSanXuat = r.NamSanXuat,
                TinhTrang = r.TinhTrang,
                TuNgay = r.TuNgay,
                DenNgay = r.DenNgay,
                Skip = r.Skip,
                Take = r.Take
            });
            return new PmisManualSearchResponse
            {
                Total = result.Total,
                Items = result.Items.Select(item => new PmisSyncPreviewItemDto
                {
                    PmisCode = item.MaThietBi,
                    DisplayName = item.TenThietBi,
                    RawData = JsonSerializer.SerializeToElement(item)
                }).ToList()
            };
        }

        var lineResult = await _pmisClient.GetLineDevicesAsync(new PmisLineDeviceSearchRequest
        {
            MaDuongDay = r.MaDuongDay,
            MaLoaiTB = r.MaLoaiTB,
            MaDonVi = r.MaDonVi,
            KemQRCode = r.KemQRCode,
            TuNgay = r.TuNgay,
            DenNgay = r.DenNgay,
            Skip = r.Skip,
            Take = r.Take
        });
        return new PmisManualSearchResponse
        {
            Total = lineResult.Total,
            Items = lineResult.Items.Select(item => new PmisSyncPreviewItemDto
            {
                PmisCode = item.MaTB,
                DisplayName = item.TenTB,
                RawData = JsonSerializer.SerializeToElement(item)
            }).ToList()
        };
    }

    private string? CurrentUserName() =>
        User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
