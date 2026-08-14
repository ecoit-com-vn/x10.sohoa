using System.Data;
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
                   OR UPPER(UserName) = UPPER(:Username))
            ORDER BY CASE WHEN SSO_USER_ID = :SsoUserId THEN 0 ELSE 1 END
            FETCH FIRST 1 ROWS ONLY",
            new { SsoUserId = NullIfEmpty(identity.UserId), Username = username });

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new SsoException(
                "AUT-006",
                $"Tài khoản '{username}' đã xác thực trên SSO nhưng chưa được tạo trong hệ thống.",
                403);
        }

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new SsoException("AUT-006", "Không tìm thấy tài khoản nội bộ tương ứng.", 403);
        if (!user.IsActive)
        {
            throw new SsoException("AUT-006", "Tài khoản nội bộ đã bị vô hiệu hóa.", 403);
        }

        if (!user.IsSsoEnabled)
        {
            throw new SsoException("AUT-006", "Tài khoản nội bộ chưa được cho phép đăng nhập SSO.", 403);
        }

        await _connection.ExecuteAsync(@"
            UPDATE APP_USER
            SET AUTH_PROVIDER = 'SSO',
                SSO_USER_ID = :SsoUserId,
                SSO_USERNAME = :SsoUsername,
                SSO_NS_ID = :SsoNsId,
                SSO_DEPT_ID = :SsoDeptId,
                SSO_ORG_ID = :SsoOrgId,
                SSO_POSITION_ID = :SsoPositionId,
                STAFF_CODE = :StaffCode,
                UpdatedAt = CURRENT_TIMESTAMP,
                UpdatedBy = :Id
            WHERE Id = :Id",
            new
            {
                Id = userId,
                SsoUserId = identity.UserId,
                SsoUsername = identity.Username,
                SsoNsId = identity.NsId,
                SsoDeptId = identity.DeptId,
                SsoOrgId = identity.OrgId,
                SsoPositionId = identity.PositionId,
                identity.StaffCode
            });

        return await _userRepository.GetByIdAsync(userId)
            ?? throw new SsoException("SSO-ACCOUNT", "Không thể đọc lại tài khoản nội bộ.", 500);
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
