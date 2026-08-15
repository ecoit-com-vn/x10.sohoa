using System.Data;
using System.Security.Cryptography;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.DTOs;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Services;

public sealed class SsoAccountService : ISsoAccountService
{
    private readonly IDbConnection _connection;
    private readonly IUserRepository _userRepository;

    public SsoAccountService(IDbConnection connection, IUserRepository userRepository)
    {
        _connection = connection;
        _userRepository = userRepository;
    }

    public async Task<User> ValidateExistingAccountAsync(SsoValidationData data)
    {
        var identity = data.Identity
            ?? throw new SsoException("AUT-002", "SSO không trả về thông tin tài khoản.");
        var username = FirstNotEmpty(identity.Username, identity.UsernameLocal)
            ?? throw new SsoException("AUT-002", "SSO không trả về tên đăng nhập.");

        if (_connection.State != ConnectionState.Open) _connection.Open();
        var userId = await _connection.QuerySingleOrDefaultAsync<string?>(@"
            SELECT Id
            FROM APP_USER
            WHERE IsDeleted = 0
              AND ((:SsoUserId IS NOT NULL AND SSO_USER_ID = :SsoUserId)
                   OR (:UsernameLocal IS NOT NULL AND UPPER(UserName) = UPPER(:UsernameLocal))
                   OR (:SsoUsername IS NOT NULL AND UPPER(UserName) = UPPER(:SsoUsername)))
            ORDER BY CASE
                         WHEN SSO_USER_ID = :SsoUserId THEN 0
                         WHEN UPPER(UserName) = UPPER(:UsernameLocal) THEN 1
                         ELSE 2
                     END
            FETCH FIRST 1 ROWS ONLY",
            new
            {
                SsoUserId = NullIfEmpty(identity.UserId),
                UsernameLocal = NullIfEmpty(identity.UsernameLocal),
                SsoUsername = NullIfEmpty(identity.Username)
            });

        User user;
        if (string.IsNullOrWhiteSpace(userId))
        {
            var organizationUnitId = await ResolveOrganizationUnitIdAsync(identity.OrgId);
            var positionId = ResolvePositionId(identity.PositionId);
            var officerRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(@"
                SELECT Id
                FROM ROLE
                WHERE UPPER(Code) = 'CB'
                  AND IsActive = 1
                FETCH FIRST 1 ROWS ONLY");
            if (!officerRoleId.HasValue)
            {
                throw new SsoException(
                    "SSO-ROLE-CONFIG",
                    "Không tìm thấy vai trò đang hoạt động có mã 'CB' để gán cho tài khoản SSO mới.",
                    500);
            }

            user = CreateSsoUser(username, identity, organizationUnitId, positionId);
            await _userRepository.CreateAsync(user);
            await _connection.ExecuteAsync(@"
                INSERT INTO USER_ROLE (UserId, RoleId)
                SELECT :UserId, :RoleId
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM USER_ROLE
                    WHERE UserId = :UserId AND RoleId = :RoleId
                )",
                new { UserId = user.Id, RoleId = officerRoleId.Value });
            userId = user.Id;
        }
        else
        {
            user = await _userRepository.GetByIdAsync(userId)
                ?? throw new SsoException("AUT-006", "Không tìm thấy tài khoản nội bộ tương ứng.", 403);
        }

        if (!user.IsActive)
        {
            throw new SsoException("AUT-006", "Tài khoản nội bộ đã bị vô hiệu hóa.", 403);
        }

        await _connection.ExecuteAsync(@"
            UPDATE APP_USER
            SET FullName = :FullName,
                Email = :Email,
                PHONE_NUMBER = :PhoneNumber,
                PositionId = :PositionId,
                PositionName = :PositionName,
                AUTH_PROVIDER = 'SSO',
                SSO_USER_ID = :SsoUserId,
                SSO_USERNAME = :SsoUsername,
                SSO_NS_ID = :SsoNsId,
                SSO_DEPT_ID = :SsoDeptId,
                SSO_ORG_ID = :SsoOrgId,
                STAFF_CODE = :StaffCode,
                IS_SSO_ENABLED = 1,
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :Id
            WHERE Id = :Id",
            new
            {
                Id = userId,
                FullName = FirstNotEmpty(identity.FullName, username) ?? username,
                Email = NullIfEmpty(identity.Email) ?? string.Empty,
                PhoneNumber = NullIfEmpty(identity.Phone),
                PositionId = ResolvePositionId(identity.PositionId),
                PositionName = NullIfEmpty(identity.PositionName),
                SsoUserId = identity.UserId,
                SsoUsername = identity.Username,
                SsoNsId = identity.NsId,
                SsoDeptId = identity.DeptId,
                SsoOrgId = identity.OrgId,
                identity.StaffCode
            });

        return await _userRepository.GetByIdAsync(userId)
            ?? throw new SsoException("SSO-ACCOUNT", "Không thể đọc lại tài khoản nội bộ.", 500);
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<long?> ResolveOrganizationUnitIdAsync(string? ssoOrgId)
    {
        var normalizedOrgId = NullIfEmpty(ssoOrgId);
        if (normalizedOrgId is null)
            return null;

        if (!long.TryParse(normalizedOrgId, out var organizationUnitId))
        {
            throw new SsoException(
                "SSO-ORG-MAPPING",
                $"orgId '{normalizedOrgId}' từ SSO không phải ID đơn vị hợp lệ.",
                500);
        }

        var exists = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ORGANIZATION_UNIT WHERE Id = :Id AND IsActive = 1 AND IsDeleted = 0",
            new { Id = organizationUnitId });
        if (exists == 0)
        {
            throw new SsoException(
                "SSO-ORG-MAPPING",
                $"Không tìm thấy đơn vị nội bộ đang hoạt động có ID = {organizationUnitId} từ orgId SSO.",
                500);
        }

        return organizationUnitId;
    }

    private static long? ResolvePositionId(string? ssoPositionId)
    {
        var normalizedPositionId = NullIfEmpty(ssoPositionId);
        if (normalizedPositionId is null)
            return null;

        if (!long.TryParse(normalizedPositionId, out var positionId))
        {
            throw new SsoException(
                "SSO-POSITION-MAPPING",
                $"positionId '{normalizedPositionId}' từ SSO không phải ID chức vụ hợp lệ.",
                500);
        }

        return positionId;
    }

    private static User CreateSsoUser(
        string username,
        SsoIdentity identity,
        long? organizationUnitId,
        long? positionId) => new()
    {
        Username = username,
        FullName = FirstNotEmpty(identity.FullName, username) ?? username,
        Email = NullIfEmpty(identity.Email) ?? string.Empty,
        PhoneNumber = NullIfEmpty(identity.Phone),
        OrganizationUnitId = organizationUnitId,
        PositionId = positionId,
        PositionName = NullIfEmpty(identity.PositionName),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
        AuthProvider = "SSO",
        SsoUserId = NullIfEmpty(identity.UserId),
        SsoUsername = NullIfEmpty(identity.Username),
        SsoNsId = NullIfEmpty(identity.NsId),
        SsoDeptId = NullIfEmpty(identity.DeptId),
        SsoOrgId = NullIfEmpty(identity.OrgId),
        StaffCode = NullIfEmpty(identity.StaffCode),
        IsSsoEnabled = true,
        IsActive = true,
        LockoutEnabled = false
    };
}
