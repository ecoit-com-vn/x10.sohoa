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
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            return new OracleConnection(connectionString);
        });
        return services;
    }

    private static void RegisterTypeHandlers()
    {
        if (_handlersRegistered) return;

        lock (_lock)
        {
            if (_handlersRegistered) return;

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

        if (value is string str && Guid.TryParse(str, out var parsedGuid))
            return parsedGuid;

        return Guid.Empty;
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
            return guid;

        if (value is string str && Guid.TryParse(str, out var parsedGuid))
            return parsedGuid;

        return null;
    }
}
