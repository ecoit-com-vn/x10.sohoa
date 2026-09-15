using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Quản lý ánh xạ mã đơn vị PMIS (maDonVi, vd. "HN0200") ↔ đơn vị thật (ORGANIZATION_UNIT) —
/// PMIS_UNIT_CODE_MAPPING (Migration0051) trước đây chỉ seed được 12 đơn vị "HN%" qua migration, chưa
/// có cách thêm đơn vị mới (vd. "PD6800 - Công ty lưới điện cao thế") ngoài việc dev tự chạy SQL tay.
///
/// Khi thêm 1 ánh xạ mới: KHÔNG tự sửa dữ liệu Trạm/Đường dây/Thiết bị đã đồng bộ trước đó bằng 1 câu
/// UPDATE/migration riêng — thay vào đó yêu cầu SyncService chạy sớm 1 lượt đồng bộ đầy đủ (xem
/// ISyncServiceClient.TriggerSyncNowAsync), lượt đồng bộ này tự tính lại UnitId cho MỌI bản ghi (kể cả
/// những bản ghi trước đó không xác định được đơn vị) vì InfrastructureRepository/EquipmentRepository.
/// UpsertFromPmisAsync luôn tra lại PMIS_UNIT_CODE_MAPPING theo mã đơn vị PMIS mới nhất mỗi lần chạy —
/// tận dụng đúng cơ chế tự hội tụ (self-healing) đã có sẵn của đồng bộ, không cần job/migration riêng.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pmis-unit-code-mapping")]
public class PmisUnitCodeMappingController : ControllerBase
{
    private readonly IPmisUnitCodeMappingRepository _repository;
    private readonly ISyncServiceClient _syncServiceClient;

    public PmisUnitCodeMappingController(IPmisUnitCodeMappingRepository repository, ISyncServiceClient syncServiceClient)
    {
        _repository = repository;
        _syncServiceClient = syncServiceClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _repository.GetAllAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePmisUnitCodeMappingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PmisUnitCode))
            return BadRequest(new { message = "Mã đơn vị PMIS không được để trống." });
        // Khớp đúng giới hạn cột PmisUnitCode VARCHAR2(50)/Note VARCHAR2(500) (Migration0051) — trả lỗi
        // rõ ràng thay vì để bubble thành ORA-12899 (value too large) không rõ nghĩa.
        if (request.PmisUnitCode.Length > 50)
            return BadRequest(new { message = "Mã đơn vị PMIS không được vượt quá 50 ký tự." });
        if (request.Note?.Length > 500)
            return BadRequest(new { message = "Ghi chú không được vượt quá 500 ký tự." });
        if (request.UnitId <= 0)
            return BadRequest(new { message = "Chưa chọn đơn vị để ánh xạ." });

        var result = await _repository.CreateAsync(request, CurrentUserName());
        switch (result.Error)
        {
            case PmisUnitCodeMappingCreateError.DuplicateCode:
                return Conflict(new { message = $"Mã đơn vị PMIS \"{request.PmisUnitCode}\" đã có ánh xạ từ trước." });
            case PmisUnitCodeMappingCreateError.UnitNotFound:
                return BadRequest(new { message = "Không tìm thấy đơn vị để ánh xạ." });
        }

        // Chạy sớm đồng bộ để tự "vá" lại Trạm/Đường dây/Thiết bị đã đồng bộ trước đó nhưng chưa xác
        // định được đơn vị — không chặn response nếu SyncService tạm gián đoạn (đã tự log cảnh báo bên
        // trong, lượt đồng bộ theo lịch thường vẫn sẽ tự sửa đúng sau đó).
        await _syncServiceClient.TriggerSyncNowAsync();

        return Ok(new { id = result.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _repository.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = "Không tìm thấy ánh xạ." });
        return NoContent();
    }

    private string? CurrentUserName() =>
        User.FindFirst("full_name")?.Value ?? User.Identity?.Name;
}
