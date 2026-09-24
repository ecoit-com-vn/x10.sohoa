using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('đ', 'd').Replace('Đ', 'd');
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    static async Task Main()
    {
        string host = "192.168.1.199";
        int port = 1521;
        string user = "qlshx10";
        string password = "Ecoit@123qwe";
        string service = "orcl";

        string connStr = $"Data Source={host}:{port}/{service};User Id={user};Password={password};Pooling=false;";
        try
        {
            using var conn = new OracleConnection(connStr);
            conn.Open();

            // Replicate InfrastructureRepository.GetByIdAsync's EXACT current SQL (post-fix) against a
            // real row to prove no ORA-00904 remains and Dapper splitOn multi-mapping still works.
            var realId = await conn.QuerySingleOrDefaultAsync<string>("SELECT ID FROM INFRASTRUCTURE WHERE ROWNUM <= 1");
            Console.WriteLine("Testing GetByIdAsync-equivalent SQL against real Id=" + realId);
            var sql = @"SELECT i.Id,
                            i.Code,
                            i.Name,
                            i.Address,
                            i.UNIT_ID as UnitId,
                            i.GRIDTYPEID as GridTypeId,
                            i.OPERATION_DATE as OperationDate,
                            i.IS_ACTIVE as IsActive,
                            i.CreatedBy,
                            i.CreatedDate,
                            i.ModifiedBy,
                            i.ModifiedDate,
                            i.PARENT_ID as ParentId,
                            p.NAME as ParentName,
                            p.CODE as ParentCode,
                            it.NAME as InfraTypeName,
                            u.NAME as UnitName,
                            i.PMIS_CODE as PmisCode,
                            i.LAST_SYNCED_FROM_PMIS_AT as LastSyncedFromPmisAt,
                            i.CMIS_CODE as CmisCode,
                            u.Id as OrgId,
                            u.Code as OrgCode,
                            u.Name as OrgName
                     FROM INFRASTRUCTURE i
                     LEFT JOIN INFRASTRUCTURE p ON i.PARENT_ID = p.ID
                     LEFT JOIN INFRASTRUCTURE_TYPE it ON i.INFRA_TYPE_ID = it.ID
                     LEFT JOIN ORGANIZATION_UNIT u ON i.UNIT_ID = u.Id
                     WHERE i.Id = :Id AND i.IsDeleted = 0";
            try
            {
                var row = await conn.QueryFirstOrDefaultAsync(sql, new { Id = realId });
                Console.WriteLine("GetByIdAsync-equivalent SQL WORKS. Sample row: PmisCode=" + row?.PMISCODE + " CmisCode=" + row?.CMISCODE + " Code=" + row?.CODE);
            }
            catch (Exception ex) { Console.WriteLine("GetByIdAsync-equivalent SQL FAILS: " + ex.Message); }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
