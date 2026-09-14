using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class PmisUnitCodeMappingRepository : IPmisUnitCodeMappingRepository
{
    private readonly IDbConnection _connection;

    public PmisUnitCodeMappingRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    private class MappingRow
    {
        public string Id { get; set; } = string.Empty;
        public string PmisUnitCode { get; set; } = string.Empty;
        public long UnitId { get; set; }
        public string? UnitName { get; set; }
        public string? UnitCode { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public async Task<IEnumerable<PmisUnitCodeMappingDto>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var rows = await _connection.QueryAsync<MappingRow>(
            @"SELECT m.Id AS Id, m.PmisUnitCode AS PmisUnitCode, m.UnitId AS UnitId, m.Note AS Note,
                     m.CreatedDate AS CreatedDate, u.Name AS UnitName, u.Code AS UnitCode
              FROM PMIS_UNIT_CODE_MAPPING m
              LEFT JOIN ORGANIZATION_UNIT u ON m.UnitId = u.Id
              WHERE m.IsDeleted = 0
              ORDER BY m.PmisUnitCode");

        return rows.Select(r => new PmisUnitCodeMappingDto
        {
            Id = Guid.Parse(r.Id),
            PmisUnitCode = r.PmisUnitCode,
            UnitId = r.UnitId,
            UnitName = r.UnitName,
            UnitCode = r.UnitCode,
            Note = r.Note,
            CreatedDate = r.CreatedDate
        });
    }

    public async Task<CreatePmisUnitCodeMappingResult> CreateAsync(CreatePmisUnitCodeMappingRequest request, string? createdBy)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        // Kiểm tra UnitId tồn tại TRƯỚC khi insert — tránh để lộ lỗi ràng buộc khoá ngoại (ORA-02291) thô
        // ra response nếu FE gửi lên 1 UnitId không còn hợp lệ (đơn vị đã bị xoá/đổi Id).
        var unitExists = await _connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT 1 FROM ORGANIZATION_UNIT WHERE Id = :UnitId AND IsDeleted = 0", new { request.UnitId });
        if (unitExists == null)
            return CreatePmisUnitCodeMappingResult.Fail(PmisUnitCodeMappingCreateError.UnitNotFound);

        var newId = Guid.Parse(EvnHanoi.Infrastructure.Database.UuidHelper.NewUuid());
        try
        {
            await _connection.ExecuteAsync(
                @"INSERT INTO PMIS_UNIT_CODE_MAPPING (Id, PmisUnitCode, UnitId, Note, CreatedBy)
                  VALUES (:Id, :PmisUnitCode, :UnitId, :Note, :CreatedBy)",
                new
                {
                    Id = newId.ToString(),
                    request.PmisUnitCode,
                    request.UnitId,
                    request.Note,
                    CreatedBy = createdBy
                });
            return CreatePmisUnitCodeMappingResult.Ok(newId);
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            // Trùng UQ_PMIS_UNIT_CODE_MAPPING_CODE — mã đơn vị PMIS này đã có ánh xạ từ trước.
            return CreatePmisUnitCodeMappingResult.Fail(PmisUnitCodeMappingCreateError.DuplicateCode);
        }
    }
}
