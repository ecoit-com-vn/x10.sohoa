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

            var journal = await conn.QueryAsync(
                "SELECT SCRIPTNAME, APPLIED FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%006%' OR SCRIPTNAME LIKE '%0012%' OR SCRIPTNAME LIKE '%0013%' ORDER BY APPLIED");
            foreach (var j in journal) Console.WriteLine($"Journal: {j.SCRIPTNAME} applied {j.APPLIED}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
