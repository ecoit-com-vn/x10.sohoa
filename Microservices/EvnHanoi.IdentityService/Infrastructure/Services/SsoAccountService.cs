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
        var username = FirstNotEmpty(identity.UsernameLocal, identity.Username)
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
            var positionId = await ResolvePositionIdAsync(identity.PositionName);
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

            user = CreateSsoUser(username, identity, organizationUnitId == null ? 1 : organizationUnitId, positionId);
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
                OrganizationUnitId = :OrganizationUnitId,
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
                OrganizationUnitId = await ResolveOrganizationUnitIdAsync(identity.OrgId),
                PositionId = await ResolvePositionIdAsync(identity.PositionName),
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

        var organizationUnitId = await _connection.QuerySingleOrDefaultAsync<long?>(@"
            SELECT Id
            FROM ORGANIZATION_UNIT
            WHERE (TO_CHAR(ORGIDSSO) = :OrgIdSso OR TO_CHAR(Id) = :OrgIdSso)
              AND IsActive = 1
              AND IsDeleted = 0
            ORDER BY CASE WHEN TO_CHAR(ORGIDSSO) = :OrgIdSso THEN 0 ELSE 1 END
            FETCH FIRST 1 ROWS ONLY",
            new { OrgIdSso = normalizedOrgId });
        if (!organizationUnitId.HasValue)
        {
            throw new SsoException(
                "SSO-ORG-MAPPING",
                $"Không tìm thấy đơn vị nội bộ đang hoạt động có ORGIDSSO hoặc Id = '{normalizedOrgId}'.",
                500);
        }

        return organizationUnitId;
    }

    private async Task<long?> ResolvePositionIdAsync(string? ssoPositionName)
    {
        var positionName = NullIfEmpty(ssoPositionName);
        if (positionName is null)
            return null;

        var positionId = await _connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT c.Id
            FROM CATALOG c
            INNER JOIN CATALOG_TYPE ct ON ct.Id = c.CatalogTypeId
            WHERE UPPER(ct.Code) = 'CHUC_VU'
              AND ct.IsDeleted = 0
              AND c.IsDeleted = 0
              AND UPPER(TRIM(c.Name)) = UPPER(TRIM(:PositionName))
            ORDER BY CASE WHEN c.Status = 1 THEN 0 ELSE 1 END, c.Id
            FETCH FIRST 1 ROWS ONLY",
            new { PositionName = positionName });
        if (positionId.HasValue)
            return positionId;

        var catalogTypeId = await _connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT Id
            FROM CATALOG_TYPE
            WHERE UPPER(Code) = 'CHUC_VU'
              AND IsDeleted = 0
            FETCH FIRST 1 ROWS ONLY");
        if (!catalogTypeId.HasValue)
        {
            throw new SsoException(
                "SSO-POSITION-CATALOG",
                "Không tìm thấy loại danh mục chức vụ có mã 'CHUC_VU'.",
                500);
        }

        string? positionCode = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidateCode = $"SSO_POS_{Guid.NewGuid().ToString("N")[..3]}";
            var exists = await _connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM CATALOG
                WHERE CatalogTypeId = :CatalogTypeId
                  AND UPPER(Code) = UPPER(:Code)",
                new { CatalogTypeId = catalogTypeId.Value, Code = candidateCode });
            if (exists == 0)
            {
                positionCode = candidateCode;
                break;
            }
        }

        if (positionCode is null)
        {
            throw new SsoException(
                "SSO-POSITION-CATALOG",
                "Không thể tạo mã chức vụ SSO không trùng lặp.",
                500);
        }

        var parameters = new DynamicParameters();
        parameters.Add("Code", positionCode);
        parameters.Add("Name", positionName);
        parameters.Add("CatalogTypeId", catalogTypeId.Value);
        parameters.Add("Description", "Tự động đồng bộ từ SSO.");
        parameters.Add("CreatedBy", "sso");
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(@"
            INSERT INTO CATALOG (
                Code,
                Name,
                CatalogTypeId,
                ParentId,
                Description,
                UnitId,
                CreatedBy,
                Priority,
                Status)
            VALUES (
                :Code,
                :Name,
                :CatalogTypeId,
                NULL,
                :Description,
                NULL,
                :CreatedBy,
                1,
                1)
            RETURNING Id INTO :Id", parameters);

        return parameters.Get<long>("Id");
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
