using System.Security.Claims;
using ClosedXML.Excel;
using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

[Authorize]
public abstract class ReportDossierControllerBase : ControllerBase
{
    private readonly IReportDossierSearchService _searchService;
    private readonly IReportDossierDetailRepository _detailRepository;
    private readonly IReportDossierRepository _repository;

    protected ReportDossierControllerBase(
        IReportDossierSearchService searchService,
        IReportDossierDetailRepository detailRepository,
        IReportDossierRepository repository)
    {
        _searchService = searchService;
        _detailRepository = detailRepository;
        _repository = repository;
    }

    protected abstract ReportDossierKind Kind { get; }
    protected abstract string ReportTitle { get; }
    protected abstract string DimensionColumnLabel { get; }

    protected UserScope ResolveUserScope()
    {
        var isAdmin = User.IsInRole("ADMIN") ||
                      User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");

        long? unitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (long.TryParse(unitIdClaim, out var userUnitId) && userUnitId > 0)
                unitId = userUnitId;
        }

        return new UserScope { IsAdmin = isAdmin, UnitId = unitId };
    }

    [HttpGet("bhs-columns")]
    public async Task<IActionResult> GetBhsColumns()
    {
        var columns = await _repository.GetBhsColumnsAsync();
        return Ok(columns);
    }

    [HttpGet("lookups/units")]
    public async Task<IActionResult> GetUnits()
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _repository.GetOrganizationUnitsAsync(scope.IsAdmin, scope.UnitId);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] long? unitId,
        [FromQuery] int? gridTypeId,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var request = BuildSearchRequest(scope, unitId, gridTypeId, infrastructureId, equipmentId, page, pageSize);
        var result = await _searchService.SearchAsync(request);
        return Ok(new { items = result.Items, totalCount = result.TotalCount, page = result.Page, pageSize = result.PageSize });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] long? unitId,
        [FromQuery] int? gridTypeId,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? equipmentId)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var bhsColumns = (await _repository.GetBhsColumnsAsync()).ToList();
        var request = BuildSearchRequest(scope, unitId, gridTypeId, infrastructureId, equipmentId, 1, 10000);
        var result = await _searchService.SearchAsync(request);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("BaoCao");

        var col = 1;
        worksheet.Cell(1, col++).Value = "STT";
        foreach (var bhs in bhsColumns)
            worksheet.Cell(1, col++).Value = bhs.Label;
        worksheet.Cell(1, col++).Value = "Đơn vị";
        worksheet.Cell(1, col++).Value = DimensionColumnLabel;
        worksheet.Cell(1, col++).Value = "Số lượng tài liệu";

        var headerRange = worksheet.Range(1, 1, 1, col - 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        var index = 1;
        foreach (var item in result.Items)
        {
            col = 1;
            worksheet.Cell(row, col++).Value = index++;
            foreach (var bhs in bhsColumns)
            {
                worksheet.Cell(row, col++).Value = item.CatalogData.TryGetValue(bhs.Key, out var val) ? val : "-";
            }
            worksheet.Cell(row, col++).Value = item.UnitName ?? "-";
            worksheet.Cell(row, col++).Value = GetDimensionValue(item);
            worksheet.Cell(row, col++).Value = item.DocumentCount;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"{SanitizeFileName(ReportTitle)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (!await _detailRepository.IsPublishedDossierAccessibleAsync(id, scope.IsAdmin ? null : scope.UnitId))
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        var detail = await _detailRepository.GetPublishedDetailAsync(id);
        if (detail is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        return Ok(detail);
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (!await _detailRepository.IsPublishedDossierAccessibleAsync(id, scope.IsAdmin ? null : scope.UnitId))
            return NotFound(new { message = "Không tìm thấy hồ sơ hoặc tài liệu." });

        var filter = new ReportDocumentFilterDto { Keyword = keyword, Page = page, PageSize = pageSize };
        var (items, totalCount) = await _detailRepository.GetDocumentsAsync(id, filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/download-url")]
    public async Task<IActionResult> GetDocumentDownloadUrl(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (!await _detailRepository.IsPublishedDossierAccessibleAsync(id, scope.IsAdmin ? null : scope.UnitId))
            return NotFound(new { message = "Không tìm thấy tài liệu." });

        var result = await _detailRepository.CreateDocumentDownloadTokenAsync(id, versionId, cancellationToken);
        if (result is null)
            return NotFound(new { message = "Không tìm thấy tài liệu." });

        return Ok(new { url = result.Url, downloadUrl = result.DownloadUrl, token = result.Token, expiresInSeconds = result.ExpiresInSeconds });
    }

    protected async Task<IActionResult> GetGridTypeLookups(long? unitId)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _repository.GetGridTypesAsync(scope.EffectiveFilterUnitId(unitId));
        return Ok(items);
    }

    protected async Task<IActionResult> GetEquipmentLookups(long? unitId)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _repository.GetEquipmentsAsync(scope.UnitId, scope.EffectiveFilterUnitId(unitId));
        return Ok(items);
    }

    protected async Task<IActionResult> GetInfrastructureLookups(long? unitId, int infraTypeId)
    {
        var scope = ResolveUserScope();
        if (!scope.IsAdmin && scope.UnitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _repository.GetInfrastructuresAsync(scope.UnitId, scope.EffectiveFilterUnitId(unitId), infraTypeId);
        return Ok(items);
    }

    private ReportDossierSearchRequest BuildSearchRequest(
        UserScope scope,
        long? unitId,
        int? gridTypeId,
        Guid? infrastructureId,
        Guid? equipmentId,
        int page,
        int pageSize)
    {
        var request = new ReportDossierSearchRequest
        {
            UnitId = scope.EffectiveFilterUnitId(unitId),
            IsAdmin = scope.IsAdmin,
            UserUnitId = scope.UnitId,
            Page = page,
            PageSize = pageSize
        };

        switch (Kind)
        {
            case ReportDossierKind.GridType:
                request.GridTypeId = gridTypeId;
                break;
            case ReportDossierKind.Equipment:
                request.EquipmentId = equipmentId;
                break;
            case ReportDossierKind.Station:
                request.InfrastructureTypeId = 1;
                request.InfrastructureId = infrastructureId;
                break;
            case ReportDossierKind.Line:
                request.InfrastructureTypeId = 2;
                request.InfrastructureId = infrastructureId;
                break;
        }

        return request;
    }

    private string GetDimensionValue(ReportDossierListItem item) =>
        Kind switch
        {
            ReportDossierKind.GridType => item.GridTypeName ?? "-",
            ReportDossierKind.Equipment => item.EquipmentName ?? "-",
            _ => item.InfrastructureName ?? "-"
        };

    private static string SanitizeFileName(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input.Replace(' ', '_');
    }
}
