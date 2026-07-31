using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog/phong")]
public class PhongController : ControllerBase
{
    private const string CatalogTypeCode = "PHONG";
    private readonly ICatalogRepository _repository;

    public PhongController(ICatalogRepository repository) => _repository = repository;

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10, string? name = null,
        string? code = null, int? status = null, long? unitId = null)
    {
        // Quản trị viên có thể tạo phông cho nhiều đơn vị nên danh sách không được
        // bó hẹp theo unit_id mặc định của tài khoản admin.
        unitId = IsAdmin() ? unitId : GetUnitIdFromClaims();
        if (!unitId.HasValue && !IsAdmin()) return UnitRequired();
        var type = await GetTypeAsync();
        if (type == null) return MissingConfiguration();
        var (items, totalCount) = await _repository.GetPhongPagedAsync(page, pageSize, type.Id, unitId,
            name, code, status);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var item = await GetAccessibleAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PhongCatalogRequest request)
    {
        var item = request.ToCatalog();
        Normalize(item);
        var validation = Validate(item);
        if (validation != null) return validation;
        var creatorUnitId = IsAdmin() ? item.UnitId : GetUnitIdFromClaims();
        if (!creatorUnitId.HasValue) return UnitRequired();
        item.UnitId = creatorUnitId;
        var type = await GetTypeAsync();
        if (type == null) return MissingConfiguration();
        item.CatalogTypeId = type.Id;
        item.ParentId = null;
        var existing = await _repository.GetByCodeForUnitAsync(type.Id, item.Code, item.UnitId.Value);
        if (existing != null) return CodeConflict(item.Code);
        var username = Username();
        var deleted = await _repository.GetDeletedByCodeAsync(type.Id, item.Code, item.UnitId, true);
        if (deleted != null)
        {
            item.Id = deleted.Id;
            item.CreatedAt = deleted.CreatedAt;
            item.CreatedBy = deleted.CreatedBy;
            item.UpdatedBy = username;
            if (await _repository.RestoreAsync(item))
                return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
            return Conflict(new { message = "Không thể khôi phục danh mục phông. Vui lòng thử lại." });
        }
        item.CreatedBy = username;
        item.CreatedAt = DateTime.UtcNow;
        try
        {
            item.Id = await _repository.CreateAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (OracleException ex) when (ex.Number == 1) { return CodeConflict(item.Code); }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] PhongCatalogRequest request)
    {
        var item = request.ToCatalog();
        Normalize(item);
        if (id != item.Id) return BadRequest(new { message = "ID không trùng khớp." });
        var validation = Validate(item);
        if (validation != null) return validation;
        var current = await GetAccessibleAsync(id);
        if (current == null) return NotFound();
        item.UnitId = current.UnitId;
        var type = await GetTypeAsync();
        if (type == null) return MissingConfiguration();
        var duplicate = await _repository.GetByCodeForUnitAsync(type.Id, item.Code, item.UnitId.Value);
        if (duplicate != null && duplicate.Id != id) return CodeConflict(item.Code);
        item.CatalogTypeId = type.Id;
        item.ParentId = null;
        item.CreatedAt = current.CreatedAt;
        item.CreatedBy = current.CreatedBy;
        item.UpdatedBy = Username();
        try { return await _repository.UpdateAsync(item) ? NoContent() : NotFound(); }
        catch (OracleException ex) when (ex.Number == 1) { return CodeConflict(item.Code); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        if (await GetAccessibleAsync(id) == null) return NotFound();
        return await _repository.DeleteAsync(id, Username()) ? NoContent() : NotFound();
    }

    [HttpPost("{id:long}/lock")]
    public Task<IActionResult> Lock(long id) => SetStatus(id, 0);

    [HttpPost("{id:long}/unlock")]
    public Task<IActionResult> Unlock(long id) => SetStatus(id, 1);

    private async Task<IActionResult> SetStatus(long id, int status)
    {
        var item = await GetAccessibleAsync(id);
        if (item == null) return NotFound();
        item.Status = status;
        item.UpdatedBy = Username();
        if (!await _repository.UpdateAsync(item)) return NotFound();
        return Ok(new { message = status == 1 ? "Đã mở khóa danh mục thành công." : "Đã khóa danh mục thành công.", status });
    }

    private async Task<Catalog?> GetAccessibleAsync(long id)
    {
        var item = await _repository.GetByIdAsync(id);
        var type = await GetTypeAsync();
        if (item == null || type == null || item.CatalogTypeId != type.Id || !item.UnitId.HasValue ||
            (!IsAdmin() && item.UnitId != GetUnitIdFromClaims()))
            return null;
        return item;
    }

    private Task<CatalogType?> GetTypeAsync() => _repository.GetCatalogTypeByCodeAsync(CatalogTypeCode);
    private string Username() => User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
    private long? GetUnitIdFromClaims() =>
        long.TryParse(User.FindFirst("unit_id")?.Value, out var unitId) ? unitId : null;
    private bool IsAdmin() => User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN") ||
        User.Claims.Any(c => c.Type == ClaimTypes.Role &&
            (c.Value.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
             c.Value.Equals("SUPER_ADMIN", StringComparison.OrdinalIgnoreCase)));
    private static void Normalize(Catalog item)
    {
        item.Code = item.Code?.Trim() ?? string.Empty;
        item.Name = item.Name?.Trim() ?? string.Empty;
        item.Description = item.Description?.Trim();
    }
    private static BadRequestObjectResult? Validate(Catalog item)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(item.Code)) errors["code"] = "Mã danh mục là bắt buộc";
        if (string.IsNullOrWhiteSpace(item.Name)) errors["name"] = "Tên danh mục là bắt buộc";
        return errors.Count == 0 ? null : new BadRequestObjectResult(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors });
    }
    private static BadRequestObjectResult UnitRequired() =>
        new(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { unitId = "Vui lòng chọn một đơn vị" } });
    private static BadRequestObjectResult CodeConflict(string code) =>
        new(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { code = $"Mã danh mục '{code}' đã tồn tại" } });
    private static ObjectResult MissingConfiguration() =>
        new ObjectResult(new { message = "Không tìm thấy cấu hình loại danh mục PHONG." }) { StatusCode = 500 };
}

public sealed class PhongCatalogRequest
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? UnitId { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Priority { get; set; } = 1;
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Status { get; set; } = 1;

    public Catalog ToCatalog() => new()
    {
        Id = Id,
        Code = Code,
        Name = Name,
        Description = Description,
        UnitId = UnitId,
        Priority = Priority,
        Status = Status
    };
}
