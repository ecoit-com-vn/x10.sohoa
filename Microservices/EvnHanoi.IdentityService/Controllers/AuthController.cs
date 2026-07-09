using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using EvnHanoi.IdentityService.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;


namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IDbConnection _connection;
    private readonly IValidator<UpdateProfileRequest> _updateProfileValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly IAvatarStorageService _avatarStorageService;

    public AuthController(
        IUserRepository userRepository,
        IConfiguration configuration,
        IDbConnection connection,
        IValidator<UpdateProfileRequest> updateProfileValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        IAvatarStorageService avatarStorageService)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _connection = connection;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
        _avatarStorageService = avatarStorageService;
    }
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromQuery] string? ticket, [FromBody] LoginRequest? request)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = _configuration["Jwt:Key"] ?? "super_secret_key_12345678901234567890";
        var key = Encoding.ASCII.GetBytes(secretKey);

        if (!string.IsNullOrEmpty(ticket))
        {
            try
            {
                SsoValidationResponse? ssoResult = null;
                
                if (ticket == "mock-sso-ticket-123456")
                {
                    ssoResult = new SsoValidationResponse
                    {
                        Status = "SUCCESS",
                        Code = "API-000",
                        Message = "Success (Simulation Mode)",
                        Data = new SsoValidationData
                        {
                            ServiceTicket = ticket,
                            Identity = new SsoIdentity
                            {
                                Username = "admin",
                                UsernameLocal = "admin",
                                FullName = "Quản trị viên Hệ thống",
                                Email = "admin@evnhanoi.vn",
                                DeptId = "281"
                            }
                        }
                    };
                }
                else
                {
                    using var httpClient = new HttpClient();
                    var ssoUrl = $"http://10.9.165.18:3020/sso/serviceValidate?ticket={ticket}&appCode=QRCODE";
                    
                    var response = await httpClient.GetAsync(ssoUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Unauthorized(new { message = "Không thể kết nối đến máy chủ SSO EVNHANOI để xác thực ticket." });
                    }
                    
                    ssoResult = await response.Content.ReadFromJsonAsync<SsoValidationResponse>();
                }
                if (ssoResult == null || ssoResult.Status != "SUCCESS" || ssoResult.Data?.Identity == null)
                {
                    var errorMsg = ssoResult?.Message ?? "Ticket không chính xác hoặc đã hết hạn.";
                    return Unauthorized(new { message = $"Xác thực SSO thất bại: {errorMsg}" });
                }
                
                var identity = ssoResult.Data.Identity;
                var username = !string.IsNullOrEmpty(identity.UsernameLocal) ? identity.UsernameLocal : identity.Username;
                
                var ssoUser = await _userRepository.GetUserByUsernameAsync(username);
                if (ssoUser == null)
                {
                    ssoUser = new EvnHanoi.IdentityService.Core.Domain.Models.User
                    {
                        Username = username,
                        FullName = identity.FullName,
                        Email = identity.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("SsoUserDefaultPassword_123!"),
                        IsActive = true,
                        OrganizationUnitId = long.TryParse(identity.DeptId, out var deptId) ? deptId : null
                    };
                    
                    ssoUser.Id = await _userRepository.CreateAsync(ssoUser);
                }
                
                var ssoRoles = await _userRepository.GetRolesByUserIdAsync(ssoUser.Id);
                var ssoClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, ssoUser.Id),
                    new Claim(ClaimTypes.Name, ssoUser.Username)
                };
                if (ssoUser.OrganizationUnitId.HasValue)
                {
                    ssoClaims.Add(new Claim("unit_id", ssoUser.OrganizationUnitId.Value.ToString()));
                }
                foreach (var r in ssoRoles)
                {
                    ssoClaims.Add(new Claim(ClaimTypes.Role, r));
                }

                var ssoTokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(ssoClaims),
                    Expires = DateTime.UtcNow.AddMinutes(60),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var ssoAccessToken = tokenHandler.CreateToken(ssoTokenDescriptor);
                
                var ssoRefreshDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, ssoUser.Id.ToString()),
                        new Claim("TokenType", "Refresh")
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                
                var ssoRefreshToken = tokenHandler.CreateToken(ssoRefreshDescriptor);

                return Ok(new 
                { 
                    AccessToken = tokenHandler.WriteToken(ssoAccessToken),
                    RefreshToken = tokenHandler.WriteToken(ssoRefreshToken)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xác thực hệ thống: {ex.Message}" });
            }
        }

        if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Username and password are required if not using SSO ticket." });
        }

        var user = await _userRepository.GetUserByUsernameAsync(request.Username);
        
        if (user == null)
        {
            return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không chính xác." });
        }

        // Kiểm tra tài khoản có đang hoạt động không
        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên." });
        }

        // Check if account is locked out
        if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingTime = user.LockoutEnd.Value - DateTime.UtcNow;
            return StatusCode(423, new 
            { 
                message = $"Tài khoản của bạn đã bị khóa do đăng nhập sai nhiều lần. Vui lòng quay lại sau {Math.Ceiling(remainingTime.TotalMinutes)} phút.",
                lockoutEnd = user.LockoutEnd.Value 
            });
        }
        string logs = BCrypt.Net.BCrypt.HashPassword(request.Password);
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            if (user.LockoutEnabled)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.AccessFailedCount = 0; // Reset counter after locking
                }
                await _userRepository.UpdateAsync(user);
            }
            
            var remainingAttempts = user.LockoutEnabled ? (5 - user.AccessFailedCount) : 5;
            var alertMsg = user.AccessFailedCount == 0 
                ? "Tài khoản của bạn đã bị khóa tạm thời 15 phút." 
                : $"Tài khoản hoặc mật khẩu không chính xác. Bạn còn {remainingAttempts} lần thử.";

            return Unauthorized(new { message = alertMsg });
        }

        // Login success, reset counters
        if (user.AccessFailedCount > 0 || user.LockoutEnd.HasValue)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _userRepository.UpdateAsync(user);
        }

        var userRoles = await _userRepository.GetRolesByUserIdAsync(user.Id);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        };
        if (user.OrganizationUnitId.HasValue)
        {
            claims.Add(new Claim("unit_id", user.OrganizationUnitId.Value.ToString()));
        }
        foreach (var r in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var accessToken = tokenHandler.CreateToken(tokenDescriptor);

        var refreshTokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("TokenType", "Refresh")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var refreshToken = tokenHandler.CreateToken(refreshTokenDescriptor);

        return Ok(new 
        { 
            AccessToken = tokenHandler.WriteToken(accessToken),
            RefreshToken = tokenHandler.WriteToken(refreshToken)
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request)
    {
        if (request == null || string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token là bắt buộc." });
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = _configuration["Jwt:Key"] ?? "super_secret_key_12345678901234567890";
        var key = Encoding.ASCII.GetBytes(secretKey);

        ClaimsPrincipal principal;
        try
        {
            principal = tokenHandler.ValidateToken(request.RefreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            }, out _);
        }
        catch
        {
            return Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn." });
        }

        if (principal.FindFirst("TokenType")?.Value != "Refresh")
        {
            return Unauthorized(new { message = "Refresh token không hợp lệ." });
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Refresh token không hợp lệ." });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản không tồn tại hoặc đã bị vô hiệu hóa." });
        }

        var userRoles = await _userRepository.GetRolesByUserIdAsync(user.Id);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        };
        if (user.OrganizationUnitId.HasValue)
        {
            claims.Add(new Claim("unit_id", user.OrganizationUnitId.Value.ToString()));
        }
        foreach (var r in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var accessToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        });

        var refreshToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("TokenType", "Refresh")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        });

        return Ok(new
        {
            AccessToken = tokenHandler.WriteToken(accessToken),
            RefreshToken = tokenHandler.WriteToken(refreshToken)
        });
    }

    /// <summary>
    /// [CHỈ DÙNG TRONG DEVELOPMENT] Khởi tạo tài khoản admin mặc định nếu chưa có trong DB.
    /// Mật khẩu mặc định: Admin@123!
    /// Xóa hoặc disable endpoint này trước khi deploy lên production.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("dev/init-admin")]
    public async Task<IActionResult> InitAdminUser()
    {
        // Chỉ cho phép trong môi trường Development
        if (!HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            return NotFound(); // Giả vờ không tồn tại trong production
        }

        try
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();

            // 1. Đảm bảo Role ADMIN và OPERATOR tồn tại
            var adminRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'ADMIN'");

            if (!adminRoleId.HasValue)
            {
                var insertRoleSql = @"
                    INSERT INTO ROLE (Code, Name, Description, CreatedBy)
                    VALUES ('ADMIN', 'Quản trị viên hệ thống', 'Tài khoản có toàn quyền trên hệ thống', 'SYSTEM')
                    RETURNING Id INTO :Id";
                var roleParams = new DynamicParameters();
                roleParams.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync(insertRoleSql, roleParams);
                adminRoleId = roleParams.Get<long>("Id");
            }

            var operatorRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'OPERATOR'");

            if (!operatorRoleId.HasValue)
            {
                var insertRoleSql = @"
                    INSERT INTO ROLE (Code, Name, Description, CreatedBy)
                    VALUES ('OPERATOR', 'Nhân viên vận hành', 'Tài khoản nhân viên vận hành hệ thống', 'SYSTEM')
                    RETURNING Id INTO :Id";
                var roleParams = new DynamicParameters();
                roleParams.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync(insertRoleSql, roleParams);
                operatorRoleId = roleParams.Get<long>("Id");
            }

            // 2. Đảm bảo APP_USER 'admin' và 'operator' tồn tại
            var adminUser = await _userRepository.GetUserByUsernameAsync("admin");
            bool isAdminNew = false;
            if (adminUser == null)
            {
                adminUser = new EvnHanoi.IdentityService.Core.Domain.Models.User
                {
                    Id = "018fc1e0-0000-0000-0000-000000000000",
                    Username = "admin",
                    FullName = "Quản trị viên Hệ thống",
                    Email = "admin@evnhanoi.vn",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
                    IsActive = true,
                    LockoutEnabled = false,
                    AccessFailedCount = 0
                };
                await _userRepository.CreateAsync(adminUser);
                isAdminNew = true;
            }
            else
            {
                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!");
                adminUser.IsActive = true;
                adminUser.LockoutEnd = null;
                adminUser.AccessFailedCount = 0;
                await _userRepository.UpdateFullAsync(adminUser);
            }

            var operatorUser = await _userRepository.GetUserByUsernameAsync("operator");
            if (operatorUser == null)
            {
                operatorUser = new EvnHanoi.IdentityService.Core.Domain.Models.User
                {
                    Id = "018fc1e0-0000-0000-0000-000000000001",
                    Username = "operator",
                    FullName = "Nhân viên Vận hành",
                    Email = "operator@evnhanoi.vn",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
                    IsActive = true,
                    LockoutEnabled = true,
                    AccessFailedCount = 0
                };
                await _userRepository.CreateAsync(operatorUser);
            }
            else
            {
                operatorUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!");
                operatorUser.IsActive = true;
                operatorUser.LockoutEnd = null;
                operatorUser.AccessFailedCount = 0;
                await _userRepository.UpdateFullAsync(operatorUser);
            }

            // 3. Gán Role cho Admin
            var hasAdminRole = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM USER_ROLE WHERE UserId = :UserId AND RoleId = :RoleId",
                new { UserId = adminUser.Id, RoleId = adminRoleId.Value });

            if (hasAdminRole == 0)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)",
                    new { UserId = adminUser.Id, RoleId = adminRoleId.Value });
            }

            // 4. Gán Role cho Operator
            var hasOperatorRole = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM USER_ROLE WHERE UserId = :UserId AND RoleId = :RoleId",
                new { UserId = operatorUser.Id, RoleId = operatorRoleId.Value });

            if (hasOperatorRole == 0)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)",
                    new { UserId = operatorUser.Id, RoleId = operatorRoleId.Value });
            }

            // 5. Đảm bảo Quyền SUPER_ADMIN tồn tại và gán cho Admin
            var hasSuperAdminPerm = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PERMISSION WHERE Code = 'SUPER_ADMIN'");

            if (hasSuperAdminPerm == 0)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy) VALUES ('admin-super-perm-uuid-111111111111', 'SUPER_ADMIN', 'Toàn quyền Hệ thống', 'Quyền quản trị tối cao', 1, 'SYSTEM')");
                await _connection.ExecuteAsync(
                    "INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName) VALUES ('admin-super-detail-uuid-111111111111', 'admin-super-perm-uuid-111111111111', '*', '*')");
            }

            var hasUserPerm = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM USER_PERMISSION WHERE UserId = :UserId AND PermissionId = 'admin-super-perm-uuid-111111111111'",
                new { UserId = adminUser.Id });

            if (hasUserPerm == 0)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO USER_PERMISSION (UserId, PermissionId) VALUES (:UserId, 'admin-super-perm-uuid-111111111111')",
                    new { UserId = adminUser.Id });
            }

            return Ok(new 
            { 
                message = isAdminNew ? "Khởi tạo tài khoản admin thành công." : "Tài khoản admin đã tồn tại. Đã reset mật khẩu thành công.",
                username = "admin",
                password = "Admin@123!",
                userId = adminUser.Id,
                note = "Tài khoản đã tự động được gán vai trò ADMIN và quyền SUPER_ADMIN!"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi khởi tạo admin: {ex.Message}" });
        }
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var user = !string.IsNullOrEmpty(userId)
            ? await _userRepository.GetByIdAsync(userId)
            : await _userRepository.GetUserByUsernameAsync(username!);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var userRoles = await _userRepository.GetRolesByUserIdAsync(user.Id);
        var userPermissions = await _userRepository.GetPermissionsByUserIdAsync(user.Id);
        return Ok(new 
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PositionId = user.PositionId,
            PositionName = user.PositionName,
            AvatarObjectKey = user.AvatarObjectKey,
            AvatarUrl = BuildAvatarUrl(user),
            UnitId = user.OrganizationUnitId,
            OrganizationUnitId = user.OrganizationUnitId,
            OrganizationUnit = user.OrganizationUnit,
            IsActive = user.IsActive,
            Roles = userRoles,
            Permissions = userPermissions
        });
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest? request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "profile", "Dữ liệu cập nhật không hợp lệ." } }
            });
        }

        var validationResult = await _updateProfileValidator.ValidateAsync(request);
        var errors = validationResult.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors
            });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        var email = request.Email.Trim();
        if (await _userRepository.EmailExistsForOtherUserAsync(email, userId))
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "email", "Email đã được sử dụng." } }
            });
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.PositionId = request.PositionId;
        user.PositionName = string.IsNullOrWhiteSpace(request.PositionName)
            ? null
            : request.PositionName.Trim();

        await _userRepository.UpdateProfileAsync(user);

        var updated = await _userRepository.GetByIdAsync(userId);
        if (updated == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng sau khi cập nhật." });
        }

        return Ok(new
        {
            Id = updated.Id,
            Username = updated.Username,
            FullName = updated.FullName,
            Email = updated.Email,
            PositionId = updated.PositionId,
            PositionName = updated.PositionName,
            AvatarObjectKey = updated.AvatarObjectKey,
            AvatarUrl = BuildAvatarUrl(updated),
            UnitId = updated.OrganizationUnitId,
            OrganizationUnitId = updated.OrganizationUnitId,
            OrganizationUnit = updated.OrganizationUnit,
            IsActive = updated.IsActive
        });
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest? request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "password", "Dữ liệu đổi mật khẩu không hợp lệ." } }
            });
        }

        var validationResult = await _changePasswordValidator.ValidateAsync(request);
        var errors = validationResult.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors
            });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã bị vô hiệu hóa." });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "currentPassword", "Mật khẩu hiện tại không đúng." } }
            });
        }

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "newPassword", "Mật khẩu mới không được trùng mật khẩu hiện tại." } }
            });
        }

        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(user.Id, newPasswordHash);
        HttpContext.SetAudit(user.Id, user.Username, $"Đổi mật khẩu tài khoản {user.Username}", "USER", AuditActions.Update);

        return Ok(new { message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpPost("avatar")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ảnh đại diện." });
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            return BadRequest(new { message = "Dung lượng ảnh đại diện không được vượt quá 2MB." });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        string objectKey;
        try
        {
            objectKey = await _avatarStorageService.UploadAvatarAsync(userId, user.OrganizationUnit?.Code, file, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var oldAvatarObjectKey = user.AvatarObjectKey;
        await _userRepository.UpdateAvatarAsync(userId, objectKey);

        if (!string.IsNullOrWhiteSpace(oldAvatarObjectKey))
        {
            await _avatarStorageService.DeleteAvatarAsync(oldAvatarObjectKey, cancellationToken);
        }

        user.AvatarObjectKey = objectKey;
        return Ok(new
        {
            message = "Cập nhật ảnh đại diện thành công.",
            avatarObjectKey = objectKey,
            avatarUrl = BuildAvatarUrl(user)
        });
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(user.AvatarObjectKey))
        {
            return NotFound();
        }

        var avatar = await _avatarStorageService.DownloadAvatarAsync(user.AvatarObjectKey, cancellationToken);
        return File(avatar.Stream, avatar.ContentType);
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarObjectKey))
        {
            await _avatarStorageService.DeleteAvatarAsync(user.AvatarObjectKey, cancellationToken);
            await _userRepository.UpdateAvatarAsync(userId, null);
        }

        return Ok(new { message = "Xóa ảnh đại diện thành công." });
    }

    private string? BuildAvatarUrl(EvnHanoi.IdentityService.Core.Domain.Models.User user)
    {
        if (string.IsNullOrWhiteSpace(user.AvatarObjectKey))
        {
            return null;
        }

        var version = Uri.EscapeDataString(user.AvatarObjectKey);
        return $"{Request.Scheme}://{Request.Host}/api/v1/auth/avatar?v={version}";
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    [Authorize]
    [BypassDynamicPermission]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var permissions = await _userRepository.GetPermissionsByUserIdAsync(userId);
        return Ok(permissions);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long? PositionId { get; set; }
    public string? PositionName { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class SsoValidationResponse
{
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public SsoValidationData? Data { get; set; }
}

public class SsoValidationData
{
    public string ServiceTicket { get; set; } = string.Empty;
    public SsoIdentity? Identity { get; set; }
}

public class SsoIdentity
{
    public string Username { get; set; } = string.Empty;
    public string UsernameLocal { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Ns_id { get; set; } = string.Empty;
    public string DeptId { get; set; } = string.Empty;
}

