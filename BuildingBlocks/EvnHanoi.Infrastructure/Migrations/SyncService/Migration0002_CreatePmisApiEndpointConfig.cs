using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Creates admin-editable endpoint URL + header configuration for the 9 PMIS pull APIs
/// (thay cho hard-code URL trong appsettings) và seed sẵn đúng 9 dòng theo tài liệu
/// "[EVNHANOI_SHHSKT] Phương án đồng bộ PMIS" — Url để trống, IsActive = 0, admin tự điền qua UI.
/// </summary>
public class Migration0002_CreatePmisApiEndpointConfig : IScript
{
    private static readonly (string Code, string DisplayName)[] SeedApis =
    [
        ("SUBSTATION_LIST", "API danh sách trạm biến áp"),
        ("LINE_LIST", "API danh sách đường dây"),
        ("SUBSTATION_DEVICE_TYPE_LIST", "API danh sách loại thiết bị TBA"),
        ("SUBSTATION_DEVICE_LIST", "API danh sách thiết bị TBA"),
        ("LINE_DEVICE_TYPE_LIST", "API danh sách loại thiết bị đường dây"),
        ("LINE_DEVICE_LIST", "API danh sách thiết bị đường dây"),
        ("DEVICE_DETAIL", "API chi tiết thiết bị"),
        ("SUBSTATION_DOCUMENT_LIST", "API danh sách tài liệu thiết bị TBA"),
        ("LINE_DOCUMENT_LIST", "API danh sách tài liệu thiết bị đường dây"),
    ];

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        Execute(command, @"
            CREATE TABLE PMIS_API_ENDPOINT_CONFIG (
                ID              VARCHAR2(36)    NOT NULL,
                API_CODE        VARCHAR2(50)    NOT NULL,
                DISPLAY_NAME    NVARCHAR2(250)  NOT NULL,
                URL             VARCHAR2(500)   NULL,
                HTTP_METHOD     VARCHAR2(10)    DEFAULT 'GET' NOT NULL,
                TIMEOUT_SECONDS NUMBER          NULL,
                IS_ACTIVE       NUMBER(1)       DEFAULT 0 NOT NULL,
                ROW_VERSION     NUMBER          DEFAULT 1 NOT NULL,
                CREATED_BY      VARCHAR2(100)   NULL,
                CREATED_DATE    TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
                MODIFIED_BY     VARCHAR2(100)   NULL,
                MODIFIED_DATE   TIMESTAMP       NULL,
                IS_DELETED      NUMBER(1)       DEFAULT 0 NOT NULL,
                CONSTRAINT PK_PMIS_API_ENDPOINT_CONFIG PRIMARY KEY (ID),
                CONSTRAINT UQ_PMIS_API_ENDPOINT_CONFIG_CODE UNIQUE (API_CODE),
                CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
                    'SUBSTATION_LIST', 'LINE_LIST', 'SUBSTATION_DEVICE_TYPE_LIST', 'SUBSTATION_DEVICE_LIST',
                    'LINE_DEVICE_TYPE_LIST', 'LINE_DEVICE_LIST', 'DEVICE_DETAIL',
                    'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST'
                )),
                CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_ACTIVE CHECK (IS_ACTIVE IN (0, 1)),
                CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_DELETED CHECK (IS_DELETED IN (0, 1))
            )");

        Execute(command, @"
            CREATE TABLE PMIS_API_ENDPOINT_HEADER (
                ID                  VARCHAR2(36)    NOT NULL,
                ENDPOINT_CONFIG_ID  VARCHAR2(36)    NOT NULL,
                HEADER_KEY          VARCHAR2(200)   NOT NULL,
                HEADER_VALUE        VARCHAR2(1000)  NULL,
                IS_SECRET           NUMBER(1)       DEFAULT 0 NOT NULL,
                CREATED_BY          VARCHAR2(100)   NULL,
                CREATED_DATE        TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
                MODIFIED_BY         VARCHAR2(100)   NULL,
                MODIFIED_DATE       TIMESTAMP       NULL,
                IS_DELETED          NUMBER(1)       DEFAULT 0 NOT NULL,
                CONSTRAINT PK_PMIS_API_ENDPOINT_HEADER PRIMARY KEY (ID),
                CONSTRAINT FK_PMIS_API_ENDPOINT_HEADER_CONFIG FOREIGN KEY (ENDPOINT_CONFIG_ID)
                    REFERENCES PMIS_API_ENDPOINT_CONFIG(ID) ON DELETE CASCADE,
                CONSTRAINT CK_PMIS_API_ENDPOINT_HEADER_SECRET CHECK (IS_SECRET IN (0, 1))
            )");

        Execute(command,
            "CREATE INDEX IDX_PMIS_API_ENDPOINT_HEADER_CONFIG ON PMIS_API_ENDPOINT_HEADER (ENDPOINT_CONFIG_ID)");

        foreach (var (code, displayName) in SeedApis)
        {
            ExecuteSeed(dbCommandFactory, code, displayName);
        }

        return string.Empty;
    }

    private static void ExecuteSeed(Func<IDbCommand> dbCommandFactory, string apiCode, string displayName)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            INSERT INTO PMIS_API_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, IS_ACTIVE)
            VALUES (:Id, :ApiCode, :DisplayName, 0)";

        AddParameter(command, "Id", Guid.CreateVersion7().ToString());
        AddParameter(command, "ApiCode", apiCode);
        AddParameter(command, "DisplayName", displayName);

        try
        {
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            // API_CODE đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
