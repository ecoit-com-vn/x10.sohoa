using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

/// <summary>Tra 1 dòng INFRASTRUCTURE (Trạm/Đường dây) theo PMIS_CODE — dùng chung bởi
/// EquipmentRepository.UpsertFromPmisAsync (resolve ParentPmisCode của Thiết bị) và
/// InfrastructureRepository.UpsertFromPmisAsync (resolve ParentPmisCode/"maCha" của Đường dây), tránh 2
/// bản sao gần như giống hệt nhau của cùng 1 câu SQL 2 cột (Id, GridTypeId).</summary>
internal static class InfrastructurePmisLookup
{
    private class Row
    {
        public string Id { get; set; } = string.Empty;
        public int? GridTypeId { get; set; }
    }

    /// <summary>null nếu không có INFRASTRUCTURE nào đang hoạt động (IsDeleted=0) khớp mã PMIS này —
    /// UPPER(TRIM(...)) ở cả 2 bên để chống lệch khoảng trắng/hoa-thường giữa dữ liệu PMIS và DB.</summary>
    internal static async Task<(string Id, int? GridTypeId)?> ResolveByPmisCodeAsync(IDbConnection connection, string pmisCode)
    {
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT Id, GRIDTYPEID AS GridTypeId FROM INFRASTRUCTURE WHERE UPPER(TRIM(PMIS_CODE)) = UPPER(TRIM(:PmisCode)) AND IsDeleted = 0",
            new { PmisCode = pmisCode });
        return row == null ? null : (row.Id, row.GridTypeId);
    }

    private class RowWithCode
    {
        public string Id { get; set; } = string.Empty;
        public int? GridTypeId { get; set; }
        public string NormalizedPmisCode { get; set; } = string.Empty;
    }

    /// <summary>Bản BATCH của <see cref="ResolveByPmisCodeAsync"/> — 1 round-trip DB cho CẢ TRANG thay vì
    /// 1 round-trip/bản ghi (xem EquipmentRepository.PrefetchUpsertLookupsAsync). Key trả về là
    /// UPPER(TRIM(pmisCode)) — gọi UPPER(TRIM(...)) đúng y hệt ở phía caller khi tra cứu dictionary để
    /// khớp chuẩn hoá 2 chiều giống ResolveByPmisCodeAsync.</summary>
    internal static async Task<Dictionary<string, (string Id, int? GridTypeId)>> ResolveManyByPmisCodesAsync(
        IDbConnection connection, IReadOnlyCollection<string> pmisCodes)
    {
        var result = new Dictionary<string, (string Id, int? GridTypeId)>();
        if (pmisCodes.Count == 0) return result;

        var rows = await connection.QueryAsync<RowWithCode>(
            @"SELECT Id, GRIDTYPEID AS GridTypeId, UPPER(TRIM(PMIS_CODE)) AS NormalizedPmisCode
              FROM INFRASTRUCTURE WHERE UPPER(TRIM(PMIS_CODE)) IN :Codes AND IsDeleted = 0",
            new { Codes = pmisCodes });
        foreach (var row in rows) result[row.NormalizedPmisCode] = (row.Id, row.GridTypeId);
        return result;
    }
}
