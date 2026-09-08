using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bảng cấu hình URL + trạng thái cho 3 API tích hợp ký số ngoài (xem
/// HUONG_DAN_TICH_HOP_KY_SO.md và KySoClient.cs) — chỉ sửa, không thêm/xoá dòng, giống hệt
/// pattern PMIS_API_ENDPOINT_CONFIG (Migration0002_CreatePmisApiEndpointConfig, SyncService).
/// Màn hình "Thiết lập đồng bộ ký số" ở admin-portal chỉ dùng để tra cứu/theo dõi cấu hình hiện tại —
/// KySoClient vẫn đọc URL từ appsettings ("Endpoints:KySo" + path hằng số), CHƯA đọc từ bảng này.
/// </summary>
public class Migration0056_CreateDigitalSignatureEndpointConfigTable : IScript
{
    private static readonly (string Code, string DisplayName, string Url)[] SeedApis =
    [
        ("KYSO_GET_SERIAL", "Lấy thông tin serial chứng thư số", "https://gwlocal.evnhanoi.vn/api/DigitalSignature/lay-thong-tin-serial-number"),
        ("KYSO_GET_SIGNATURE_IMAGE", "Lấy ảnh chữ ký", "https://gwlocal.evnhanoi.vn/hrms/api/Hrms/lay-anh-chu-ky"),
        ("KYSO_SIGN_PDF", "Ký số file PDF (đóng dấu ảnh chữ ký)", "https://gwlocal.evnhanoi.vn/kyso/api/DigitalSignature/sign-pdf-base64-image"),
    ];

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        Execute(command, @"
            CREATE TABLE DIGITAL_SIGNATURE_ENDPOINT_CONFIG (
                ID              VARCHAR2(36)    NOT NULL,
                API_CODE        VARCHAR2(50)    NOT NULL,
                DISPLAY_NAME    NVARCHAR2(250)  NOT NULL,
                URL             VARCHAR2(500)   NULL,
                IS_ACTIVE       NUMBER(1)       DEFAULT 1 NOT NULL,
                ROW_VERSION     NUMBER          DEFAULT 1 NOT NULL,
                CREATED_BY      VARCHAR2(100)   NULL,
                CREATED_DATE    TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
                MODIFIED_BY     VARCHAR2(100)   NULL,
                MODIFIED_DATE   TIMESTAMP       NULL,
                IS_DELETED      NUMBER(1)       DEFAULT 0 NOT NULL,
                CONSTRAINT PK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG PRIMARY KEY (ID),
                CONSTRAINT UQ_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_CODE UNIQUE (API_CODE),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
                    'KYSO_GET_SERIAL', 'KYSO_GET_SIGNATURE_IMAGE', 'KYSO_SIGN_PDF'
                )),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_ACTIVE CHECK (IS_ACTIVE IN (0, 1)),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_DELETED CHECK (IS_DELETED IN (0, 1))
            )");

        foreach (var (code, displayName, url) in SeedApis)
        {
            ExecuteSeed(dbCommandFactory, code, displayName, url);
        }

        return string.Empty;
    }

    private static void ExecuteSeed(Func<IDbCommand> dbCommandFactory, string apiCode, string displayName, string url)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            INSERT INTO DIGITAL_SIGNATURE_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, URL, IS_ACTIVE)
            VALUES (:Id, :ApiCode, :DisplayName, :Url, 1)";

        AddParameter(command, "Id", Guid.CreateVersion7().ToString());
        AddParameter(command, "ApiCode", apiCode);
        AddParameter(command, "DisplayName", displayName);
        AddParameter(command, "Url", url);

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
