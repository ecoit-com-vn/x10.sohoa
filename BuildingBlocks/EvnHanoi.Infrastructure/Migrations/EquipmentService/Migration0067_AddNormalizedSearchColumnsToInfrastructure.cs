using DbUp.Engine;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm 2 cột NORMALIZED_CODE/NORMALIZED_NAME vào INFRASTRUCTURE — ghi sẵn kết quả bỏ dấu tiếng Việt
/// (chữ thường) lúc upsert/tạo/sửa, thay vì tính 268 hàm REPLACE() lồng nhau (xem
/// InfrastructureRepository.RemoveDiacritics, 67 cặp ký tự × 2 cột Code+Name = 268 REPLACE) MỖI LẦN có
/// keyword tìm kiếm trên màn Danh mục Trạm/Đường dây (audit hiệu năng PMIS 2026-09-24) — full table scan
/// ~38k dòng × 268 hàm string mỗi ký tự người dùng gõ.
///
/// LƯU Ý QUAN TRỌNG: đây KHÔNG giải quyết gốc full-table-scan (vẫn LIKE '%...%' wildcard đầu, B-tree index
/// không dùng được cho leading wildcard dù có index trên NORMALIZED_CODE) — chỉ giảm chi phí CPU mỗi dòng
/// từ "268 hàm string" xuống "so sánh string thường", đủ dùng ở quy mô hiện tại (~38k dòng). Giải quyết gốc
/// cần Oracle Text hoặc chuyển sang Elasticsearch — nằm ngoài phạm vi migration này.
///
/// BACKFILL: tính RemoveDiacritics ở PHÍA .NET (không phải SQL REPLACE lồng nhau) rồi ghi bằng Oracle
/// ARRAY BIND (OracleCommand.ArrayBindCount) — MỖI LÔ ~5000 dòng gửi lên trong ĐÚNG 1 round-trip DB. Đã
/// thử 2 cách khác trước khi chọn cách này, cả 2 đều thất bại thật khi test trên Oracle dev:
///   1. UPDATE từng dòng riêng lẻ từ .NET (~38k round-trip qua mạng) — quá chậm, phải huỷ giữa chừng.
///   2. UPDATE set-based với SQL REPLACE() lồng 61 tầng ngay trong câu UPDATE (Oracle tự tính server-side)
///      — TƯỞNG sẽ nhanh nhất (đúng 1 round-trip/lô) nhưng THỰC TẾ treo >14 phút không tiến triển (có khả
///      năng Oracle SQL parser/optimizer xử lý cực chậm với expression lồng quá sâu) — đã phải huỷ.
/// Cách array-bind hiện tại đã ĐO THẬT: toàn bộ ~38.815 dòng backfill xong trong ~4.3 giây (8 lô × ~400-
/// 600ms/lô) trên chính Oracle dev này — xem lịch sử fix trong project memory nếu cần đối chiếu.
/// </summary>
public class Migration0067_AddNormalizedSearchColumnsToInfrastructure : IScript
{
    private static readonly (string From, string To)[] VietnameseDiacriticsMap =
    [
        ("à","a"),("á","a"),("ả","a"),("ã","a"),("ạ","a"),
        ("ă","a"),("ắ","a"),("ằ","a"),("ẳ","a"),("ẵ","a"),("ặ","a"),
        ("â","a"),("ấ","a"),("ầ","a"),("ẩ","a"),("ẫ","a"),("ậ","a"),
        ("è","e"),("é","e"),("ẻ","e"),("ẽ","e"),("ẹ","e"),
        ("ê","e"),("ế","e"),("ề","e"),("ể","e"),("ễ","e"),("ệ","e"),
        ("ì","i"),("í","i"),("ỉ","i"),("ĩ","i"),("ị","i"),
        ("ò","o"),("ó","o"),("ỏ","o"),("õ","o"),("ọ","o"),
        ("ô","o"),("ố","o"),("ồ","o"),("ổ","o"),("ỗ","o"),("ộ","o"),
        ("ơ","o"),("ớ","o"),("ờ","o"),("ở","o"),("ỡ","o"),("ợ","o"),
        ("ù","u"),("ú","u"),("ủ","u"),("ũ","u"),("ụ","u"),
        ("ư","u"),("ứ","u"),("ừ","u"),("ử","u"),("ữ","u"),("ự","u"),
        ("ỳ","y"),("ý","y"),("ỷ","y"),("ỹ","y"),("ỵ","y"),
        ("đ","d")
    ];

    // Bản sao CHÍNH XÁC của InfrastructureRepository.RemoveDiacritics — migration chạy độc lập, không
    // tham chiếu được code của Microservices, nên phải tự chuẩn hoá y hệt khi backfill.
    private static string RemoveDiacritics(string? text)
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

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER TABLE INFRASTRUCTURE ADD NORMALIZED_CODE VARCHAR2(100) NULL", "ORA-01430");
        Execute(dbCommandFactory, "ALTER TABLE INFRASTRUCTURE ADD NORMALIZED_NAME VARCHAR2(500) NULL", "ORA-01430");
        Execute(dbCommandFactory, "CREATE INDEX IX_INFRASTRUCTURE_NORM_CODE ON INFRASTRUCTURE (NORMALIZED_CODE)", "ORA-00955");

        Backfill(dbCommandFactory);

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql, string ignoreOraCode)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains(ignoreOraCode, StringComparison.OrdinalIgnoreCase))
        {
            // Cột/index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }
    }

    private static void Backfill(Func<IDbCommand> dbCommandFactory)
    {
        while (true)
        {
            var ids = new List<string>();
            var codes = new List<string?>();
            var names = new List<string?>();

            using (var selectCmd = dbCommandFactory())
            {
                selectCmd.CommandText = "SELECT ID, CODE, NAME FROM INFRASTRUCTURE WHERE NORMALIZED_CODE IS NULL FETCH FIRST 5000 ROWS ONLY";
                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    ids.Add(reader.GetString(0));
                    codes.Add(reader.IsDBNull(1) ? null : reader.GetString(1));
                    names.Add(reader.IsDBNull(2) ? null : reader.GetString(2));
                }
            }

            if (ids.Count == 0) break;

            var normCodes = new string[ids.Count];
            var normNames = new string[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                normCodes[i] = RemoveDiacritics(codes[i]);
                normNames[i] = RemoveDiacritics(names[i]);
            }

            using var updateCmd = (OracleCommand)dbCommandFactory();
            updateCmd.CommandText = "UPDATE INFRASTRUCTURE SET NORMALIZED_CODE = :normCode, NORMALIZED_NAME = :normName WHERE ID = :id";
            updateCmd.ArrayBindCount = ids.Count;
            updateCmd.Parameters.Add(new OracleParameter("normCode", OracleDbType.Varchar2, normCodes, ParameterDirection.Input));
            updateCmd.Parameters.Add(new OracleParameter("normName", OracleDbType.Varchar2, normNames, ParameterDirection.Input));
            updateCmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2, ids.ToArray(), ParameterDirection.Input));
            updateCmd.ExecuteNonQuery();
        }
    }
}
