using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Users WHERE Username = :Username AND IsActive = 1";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });
    }
}
