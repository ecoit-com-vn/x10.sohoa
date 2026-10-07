using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main(string[] args)
    {
        using var conn = new OracleConnection("Data Source=192.168.1.199:1521/orcl;User Id=qlshx10;Password=Ecoit@123qwe;Pooling=false;Connection Timeout=20;");
        conn.Open();
        var mode = args.Length > 0 ? args[0] : "show";
        if (mode == "on")
        {
            var list = await conn.QuerySingleAsync<string>("SELECT URL FROM PMIS_API_ENDPOINT_CONFIG WHERE API_CODE='LINE_DOCUMENT_LIST'");
            var host = new Uri(list).GetLeftPart(UriPartial.Authority);
            await conn.ExecuteAsync("UPDATE PMIS_API_ENDPOINT_CONFIG SET URL=:U, IS_ACTIVE=1 WHERE API_CODE='DOCUMENT_FILE_DOWNLOAD'", new { U = host + "/api/PmisDongBo/TaiFileTaiLieu" });
            Console.WriteLine("BAT: " + host + "/api/PmisDongBo/TaiFileTaiLieu");
        }
        else if (mode == "off")
        {
            await conn.ExecuteAsync("UPDATE PMIS_API_ENDPOINT_CONFIG SET IS_ACTIVE=0, URL=NULL WHERE API_CODE='DOCUMENT_FILE_DOWNLOAD'");
            Console.WriteLine("TAT");
        }
        else
        {
            foreach (var r in await conn.QueryAsync("SELECT API_CODE, URL, HTTP_METHOD, TIMEOUT_SECONDS, IS_ACTIVE, PAGE_SIZE FROM PMIS_API_ENDPOINT_CONFIG WHERE API_CODE IN ('DOCUMENT_FILE_DOWNLOAD','SUBSTATION_DOCUMENT_LIST','LINE_DOCUMENT_LIST')"))
                Console.WriteLine($"{r.API_CODE} url={r.URL} {r.HTTP_METHOD} timeout={r.TIMEOUT_SECONDS} active={r.IS_ACTIVE} page={r.PAGE_SIZE}");
            foreach (var r in await conn.QueryAsync("SELECT STATUSCODE, ISSUCCESS, COUNT(*) AS N, MAX(SUBSTR(URL,1,90)) AS URL FROM PMIS_API_CALL_LOG WHERE APICODE='DOCUMENT_FILE_DOWNLOAD' GROUP BY STATUSCODE, ISSUCCESS"))
                Console.WriteLine($"call_log status={r.STATUSCODE} ok={r.ISSUCCESS} n={r.N} {r.URL}");
            foreach (var r in await conn.QueryAsync("SELECT FILE_STATUS, COUNT(*) AS N FROM PMIS_DOCUMENT WHERE ISDELETED=0 GROUP BY FILE_STATUS"))
                Console.WriteLine($"doc {r.FILE_STATUS} = {r.N}");
            foreach (var r in await conn.QueryAsync("SELECT SUBSTR(FILE_LAST_ERROR,1,170) AS E, COUNT(*) AS N FROM PMIS_DOCUMENT WHERE FILE_ATTEMPTS>0 GROUP BY SUBSTR(FILE_LAST_ERROR,1,170) ORDER BY 2 DESC FETCH FIRST 3 ROWS ONLY"))
                Console.WriteLine($"err n={r.N}: {r.E}");
        }
    }
}
