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
}
