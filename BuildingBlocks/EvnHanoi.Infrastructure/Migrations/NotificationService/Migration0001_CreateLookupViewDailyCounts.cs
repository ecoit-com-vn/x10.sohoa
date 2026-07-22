using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.NotificationService;

/// <summary>
/// LOOKUP_VIEW_DAILY_COUNTS — số liệu tổng hợp lượt xem hồ sơ/tài liệu theo NGÀY, từ các menu tra cứu tìm kiếm
/// (Tìm kiếm fulltext, Tra cứu hồ sơ thiết bị, Tra cứu Trạm biến áp).
/// Không lưu chi tiết từng lượt xem (ai xem lúc nào) — chỉ cần tổng theo ngày để trả lời báo cáo
/// (tổng theo khoảng ngày, tổng theo tháng để tính tăng trưởng, tổng theo hồ sơ).
/// Mỗi lượt xem = MERGE (upsert) cộng dồn VIEW_COUNT cho (DOSSIER_ID, ENTITY_TYPE, VIEW_DATE) —
/// số dòng bị chặn bởi (số hồ sơ từng được xem) × (số ngày có hoạt động), không tăng theo từng lượt xem.
/// </summary>
public class Migration0001_CreateLookupViewDailyCounts : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-0{code}") || ex.Message.Contains($"ORA-{code}"))
                    {
                        ignored = true;
                        break;
                    }
                }
                if (!ignored)
                {
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                }
            }
        }

        ExecuteNonQuery(@"
            CREATE TABLE LOOKUP_VIEW_DAILY_COUNTS (
                DOSSIER_ID    VARCHAR2(36)  NOT NULL,
                ENTITY_TYPE   VARCHAR2(20)  NOT NULL,
                VIEW_DATE     DATE          NOT NULL,
                VIEW_COUNT    NUMBER        DEFAULT 1 NOT NULL,
                CREATED_DATE  TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                MODIFIED_DATE TIMESTAMP     NULL,
                CONSTRAINT pk_lookup_view_daily_counts PRIMARY KEY (DOSSIER_ID, ENTITY_TYPE, VIEW_DATE)
            )", 955);

        ExecuteNonQuery("CREATE INDEX IDX_LOOKUP_VIEW_DAILY_DATE ON LOOKUP_VIEW_DAILY_COUNTS(VIEW_DATE)", 955, 1408);

        return string.Empty;
    }
}
