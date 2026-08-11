using System;
using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.Infrastructure.Database;

public static class DapperServiceExtensions
{
    private static bool _handlersRegistered = false;
    private static readonly object _lock = new();

    public static IServiceCollection AddDapperInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterTypeHandlers();

        services.AddScoped<IDbConnection>(sp =>
        {
            var connectionString = EnsureConnectionPooling(configuration.GetConnectionString("DefaultConnection"));
            return new OracleConnection(connectionString);
        });
        return services;
    }

    private static string EnsureConnectionPooling(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        var builder = new OracleConnectionStringBuilder(connectionString);
        if (!builder.Pooling)
        {
            builder.Pooling = true;
            if (builder.MinPoolSize <= 0) builder.MinPoolSize = 5;
            if (builder.MaxPoolSize <= 0) builder.MaxPoolSize = 50;
        }

        if (builder.ConnectionTimeout <= 15)
            builder.ConnectionTimeout = 60;

        builder.ValidateConnection = true;

        if (builder.ConnectionLifeTime <= 0)
            builder.ConnectionLifeTime = 300;

        return builder.ConnectionString;
    }

    private static void RegisterTypeHandlers()
    {
        if (_handlersRegistered) return;

        lock (_lock)
        {
            if (_handlersRegistered) return;

            // BẮT BUỘC phải RemoveTypeMap TRƯỚC khi AddTypeHandler cho Guid: SqlMapper.LookupDbType
            // tra `typeMap` (bảng built-in của Dapper, có sẵn Guid -> DbType.Guid) TRƯỚC khi tra
            // `typeHandlers`. Nếu còn entry đó, Dapper sinh mã gán parameter.DbType = DbType.Guid mà
            // OracleParameter không nhận (ArgumentException "Value does not fall within the expected
            // range"), và GuidTypeHandler.SetValue bên dưới KHÔNG bao giờ được gọi — nghĩa là mọi
            // tham số Guid gửi xuống Oracle đều lỗi, kể cả trong mệnh đề WHERE. Chiều đọc
            // (Guid <- VARCHAR2(36)) vẫn dùng handler nên trước đây lỗi này chỉ lộ ra khi ghi.
            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));

            SqlMapper.AddTypeHandler(new GuidTypeHandler());
            SqlMapper.AddTypeHandler(new NullableGuidTypeHandler());
            _handlersRegistered = true;
        }
    }
}

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.DbType = DbType.AnsiString;
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value)
    {
        if (value == null || value == DBNull.Value)
            return Guid.Empty;

        if (value is Guid guid)
            return guid;

        if (value is string str && Guid.TryParse(str.Trim(), out var parsedGuid))
            return parsedGuid;

        var text = value.ToString();
        return !string.IsNullOrWhiteSpace(text) && Guid.TryParse(text.Trim(), out var parsed)
            ? parsed
            : Guid.Empty;
    }
}

public class NullableGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
{
    public override void SetValue(IDbDataParameter parameter, Guid? value)
    {
        parameter.DbType = DbType.AnsiString;
        parameter.Value = value?.ToString() ?? (object)DBNull.Value;
    }

    public override Guid? Parse(object value)
    {
        if (value == null || value == DBNull.Value)
            return null;

        if (value is Guid guid)
            return guid == Guid.Empty ? null : guid;

        if (value is string str)
            return Guid.TryParse(str.Trim(), out var parsedGuid) ? parsedGuid : null;

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return Guid.TryParse(text.Trim(), out var parsed) ? parsed : null;
    }
}
