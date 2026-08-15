using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Creates synchronization configuration, run history, and run detail tables.
/// </summary>
public class Migration0001_CreateSyncScheduleTables : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        Execute(command, @"
            CREATE TABLE SYNC_CONFIG (
                ID                  VARCHAR2(36)    NOT NULL,
                OBJECT_TYPE         VARCHAR2(30)    NOT NULL,
                FREQUENCY_VALUE     NUMBER          NOT NULL,
                FREQUENCY_UNIT      VARCHAR2(20)    NOT NULL,
                IS_ENABLED          NUMBER(1)       DEFAULT 1 NOT NULL,
                LAST_SYNC_AT        TIMESTAMP       NULL,
                NEXT_SYNC_AT        TIMESTAMP       NULL,
                CREATED_AT          TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
                CREATED_BY          VARCHAR2(36)    NULL,
                UPDATED_AT          TIMESTAMP       NULL,
                UPDATED_BY          VARCHAR2(36)    NULL,
                CONSTRAINT PK_SYNC_CONFIG PRIMARY KEY (ID),
                CONSTRAINT UQ_SYNC_CONFIG_OBJECT_TYPE UNIQUE (OBJECT_TYPE),
                CONSTRAINT CK_SYNC_CONFIG_OBJECT_TYPE
                    CHECK (OBJECT_TYPE IN ('SUBSTATION', 'TRANSMISSION_LINE', 'EQUIPMENT')),
                CONSTRAINT CK_SYNC_CONFIG_FREQUENCY_VALUE CHECK (FREQUENCY_VALUE > 0),
                CONSTRAINT CK_SYNC_CONFIG_FREQUENCY_UNIT
                    CHECK (FREQUENCY_UNIT IN ('MINUTE', 'HOUR', 'DAY')),
                CONSTRAINT CK_SYNC_CONFIG_IS_ENABLED CHECK (IS_ENABLED IN (0, 1))
            )");

        Execute(command, @"
            CREATE TABLE SYNC_HISTORY (
                ID                  VARCHAR2(36)     NOT NULL,
                SYNC_CONFIG_ID      VARCHAR2(36)     NOT NULL,
                OBJECT_TYPE         VARCHAR2(30)     NOT NULL,
                SYNC_TYPE           VARCHAR2(20)     NOT NULL,
                START_TIME          TIMESTAMP        NOT NULL,
                END_TIME            TIMESTAMP        NULL,
                STATUS              VARCHAR2(20)     NOT NULL,
                TOTAL_RECORDS       NUMBER           DEFAULT 0 NOT NULL,
                SUCCESS_RECORDS     NUMBER           DEFAULT 0 NOT NULL,
                FAILED_RECORDS      NUMBER           DEFAULT 0 NOT NULL,
                ERROR_MESSAGE       NVARCHAR2(2000)  NULL,
                CREATED_BY          VARCHAR2(36)     NULL,
                CONSTRAINT PK_SYNC_HISTORY PRIMARY KEY (ID),
                CONSTRAINT FK_SYNC_HISTORY_CONFIG
                    FOREIGN KEY (SYNC_CONFIG_ID) REFERENCES SYNC_CONFIG(ID),
                CONSTRAINT CK_SYNC_HISTORY_OBJECT_TYPE
                    CHECK (OBJECT_TYPE IN ('SUBSTATION', 'TRANSMISSION_LINE', 'EQUIPMENT')),
                CONSTRAINT CK_SYNC_HISTORY_SYNC_TYPE CHECK (SYNC_TYPE IN ('AUTO', 'MANUAL')),
                CONSTRAINT CK_SYNC_HISTORY_STATUS
                    CHECK (STATUS IN ('RUNNING', 'SUCCESS', 'FAILED')),
                CONSTRAINT CK_SYNC_HISTORY_COUNTS
                    CHECK (TOTAL_RECORDS >= 0 AND SUCCESS_RECORDS >= 0 AND FAILED_RECORDS >= 0)
            )");

        Execute(command, @"
            CREATE TABLE SYNC_HISTORY_DETAIL (
                ID                  VARCHAR2(36)     NOT NULL,
                SYNC_HISTORY_ID     VARCHAR2(36)     NOT NULL,
                SOURCE_ID           VARCHAR2(100)    NULL,
                SOURCE_CODE         VARCHAR2(100)    NULL,
                SOURCE_NAME         NVARCHAR2(500)   NULL,
                TARGET_ID           VARCHAR2(36)     NULL,
                ACTION_TYPE         VARCHAR2(20)     NOT NULL,
                STATUS              VARCHAR2(20)     NOT NULL,
                DATA_CONTENT        CLOB             NULL,
                ERROR_MESSAGE       NVARCHAR2(2000)  NULL,
                SYNC_TIME           TIMESTAMP        DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT PK_SYNC_HISTORY_DETAIL PRIMARY KEY (ID),
                CONSTRAINT FK_SYNC_HISTORY_DETAIL_HISTORY
                    FOREIGN KEY (SYNC_HISTORY_ID) REFERENCES SYNC_HISTORY(ID) ON DELETE CASCADE,
                CONSTRAINT CK_SYNC_HISTORY_DETAIL_ACTION
                    CHECK (ACTION_TYPE IN ('CREATE', 'UPDATE', 'SKIP')),
                CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS
                    CHECK (STATUS IN ('SUCCESS', 'FAILED'))
            )");

        Execute(command,
            "CREATE INDEX IDX_SYNC_HISTORY_CONFIG_TIME ON SYNC_HISTORY (SYNC_CONFIG_ID, START_TIME DESC)");
        Execute(command,
            "CREATE INDEX IDX_SYNC_HISTORY_OBJECT_TIME ON SYNC_HISTORY (OBJECT_TYPE, START_TIME DESC)");
        Execute(command,
            "CREATE INDEX IDX_SYNC_HISTORY_DETAIL_RUN ON SYNC_HISTORY_DETAIL (SYNC_HISTORY_ID)");
        Execute(command,
            "CREATE INDEX IDX_SYNC_HISTORY_DETAIL_SOURCE ON SYNC_HISTORY_DETAIL (SOURCE_ID)");

        return string.Empty;
    }

    private static void Execute(IDbCommand command, string sql)
    {
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // The table, constraint, or index already exists.
        }
    }
}
