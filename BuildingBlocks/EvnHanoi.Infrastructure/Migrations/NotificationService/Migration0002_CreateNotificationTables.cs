using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.NotificationService;

/// <summary>
/// NOTIFICATIONS — nội dung thông báo dùng chung (1 hồ sơ/thiết bị chuyển bước có thể phát sinh 1 row
/// nhưng gửi cho nhiều người).
/// NOTIFICATION_RECIPIENTS — trạng thái đọc/xóa RIÊNG theo từng người nhận, để xóa của người này
/// không ảnh hưởng người khác nhận cùng 1 thông báo.
/// </summary>
public class Migration0002_CreateNotificationTables : IScript
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
            CREATE TABLE NOTIFICATIONS (
                ID                  VARCHAR2(36)   NOT NULL,
                NOTIFICATION_TYPE   VARCHAR2(50)   NOT NULL,
                TITLE               NVARCHAR2(255) NOT NULL,
                BODY                NVARCHAR2(2000) NULL,
                RELATED_ENTITY_TYPE VARCHAR2(50)   NULL,
                RELATED_ENTITY_ID   VARCHAR2(36)   NULL,
                CREATED_AT          TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                CREATED_BY_USER_ID  VARCHAR2(36)   NULL,
                CONSTRAINT pk_notifications PRIMARY KEY (ID)
            )", 955);

        ExecuteNonQuery(@"
            CREATE TABLE NOTIFICATION_RECIPIENTS (
                ID              VARCHAR2(36) NOT NULL,
                NOTIFICATION_ID VARCHAR2(36) NOT NULL,
                USER_ID         VARCHAR2(36) NOT NULL,
                IS_READ         NUMBER(1)   DEFAULT 0 NOT NULL,
                READ_AT         TIMESTAMP   NULL,
                IS_DELETED      NUMBER(1)   DEFAULT 0 NOT NULL,
                DELETED_AT      TIMESTAMP   NULL,
                CREATED_AT      TIMESTAMP   DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT pk_notification_recipients PRIMARY KEY (ID),
                CONSTRAINT fk_notification_recipients_notif FOREIGN KEY (NOTIFICATION_ID)
                    REFERENCES NOTIFICATIONS (ID)
            )", 955);

        ExecuteNonQuery(
            "CREATE INDEX IDX_NOTIF_RECIPIENT_USER ON NOTIFICATION_RECIPIENTS(USER_ID, IS_DELETED, IS_READ)",
            955, 1408);

        ExecuteNonQuery(
            "CREATE INDEX IDX_NOTIF_RECIPIENT_NOTIF ON NOTIFICATION_RECIPIENTS(NOTIFICATION_ID)",
            955, 1408);

        return string.Empty;
    }
}
