using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UserGuideRepository : IUserGuideRepository
{
    private readonly IDbConnection _connection;

    public UserGuideRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<UserGuide>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserGuide.Id)},
                   {nameof(UserGuide.RoleName)},
                   {nameof(UserGuide.FileName)},
                   {nameof(UserGuide.ObjectKey)},
                   {nameof(UserGuide.FileSize)},
                   {nameof(UserGuide.ContentType)},
                   {nameof(UserGuide.CreatedAt)},
                   {nameof(UserGuide.CreatedBy)},
                   {nameof(UserGuide.UpdatedAt)},
                   {nameof(UserGuide.UpdatedBy)}
            FROM USER_GUIDE
            ORDER BY {nameof(UserGuide.CreatedAt)} DESC";
        return await _connection.QueryAsync<UserGuide>(sql);
    }

    public async Task<UserGuide?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(UserGuide.Id)},
                   {nameof(UserGuide.RoleName)},
                   {nameof(UserGuide.FileName)},
                   {nameof(UserGuide.ObjectKey)},
                   {nameof(UserGuide.FileSize)},
                   {nameof(UserGuide.ContentType)},
                   {nameof(UserGuide.CreatedAt)},
                   {nameof(UserGuide.CreatedBy)},
                   {nameof(UserGuide.UpdatedAt)},
                   {nameof(UserGuide.UpdatedBy)}
            FROM USER_GUIDE
            WHERE {nameof(UserGuide.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<UserGuide>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(UserGuide guide)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            INSERT INTO USER_GUIDE (
                {nameof(UserGuide.RoleName)},
                {nameof(UserGuide.FileName)},
                {nameof(UserGuide.ObjectKey)},
                {nameof(UserGuide.FileSize)},
                {nameof(UserGuide.ContentType)},
                {nameof(UserGuide.CreatedBy)}
            )
            VALUES (:RoleName, :FileName, :ObjectKey, :FileSize, :ContentType, :CreatedBy)
            RETURNING {nameof(UserGuide.Id)} INTO :Id";

        var parameters = new DynamicParameters();
        parameters.Add("RoleName", guide.RoleName);
        parameters.Add("FileName", guide.FileName);
        parameters.Add("ObjectKey", guide.ObjectKey);
        parameters.Add("FileSize", guide.FileSize);
        parameters.Add("ContentType", guide.ContentType);
        parameters.Add("CreatedBy", guide.CreatedBy);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(UserGuide guide)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE USER_GUIDE
            SET {nameof(UserGuide.RoleName)} = :RoleName,
                {nameof(UserGuide.FileName)} = :FileName,
                {nameof(UserGuide.ObjectKey)} = :ObjectKey,
                {nameof(UserGuide.FileSize)} = :FileSize,
                {nameof(UserGuide.ContentType)} = :ContentType,
                {nameof(UserGuide.UpdatedAt)} = SYSTIMESTAMP,
                {nameof(UserGuide.UpdatedBy)} = :UpdatedBy
            WHERE {nameof(UserGuide.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            guide.RoleName,
            guide.FileName,
            guide.ObjectKey,
            guide.FileSize,
            guide.ContentType,
            guide.UpdatedBy,
            guide.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $"DELETE FROM USER_GUIDE WHERE {nameof(UserGuide.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
