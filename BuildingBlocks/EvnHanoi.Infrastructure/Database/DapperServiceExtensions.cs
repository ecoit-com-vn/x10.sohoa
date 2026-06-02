using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.Infrastructure.Database;

public static class DapperServiceExtensions
{
    public static IServiceCollection AddDapperInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDbConnection>(sp =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            return new OracleConnection(connectionString);
        });
        return services;
    }
}
