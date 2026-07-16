using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Thêm AvatarObjectKey vào APP_USER (idempotent — bỏ qua nếu cột đã tồn tại).
/// Thay cho script SQL 0024 thuần ALTER (ORA-01430 khi chạy lại).
/// </summary>
public class Migration0024_AddAvatarObjectKeyToAppUser : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            try
            {
                cmd.CommandText = "ALTER TABLE APP_USER ADD AvatarObjectKey VARCHAR2(1024) NULL";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // ORA-01430: column being added already exists in table
                if (!ex.Message.Contains("ORA-01430") && !ex.Message.Contains("ORA-1430"))
                {
                    throw new Exception($"Failed adding AvatarObjectKey: {ex.Message}", ex);
                }
            }
        }

        return string.Empty;
    }
}
