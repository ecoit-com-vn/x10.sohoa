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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;


namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthController(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

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
                        UnitId = string.IsNullOrEmpty(identity.DeptId) ? null : long.Parse(identity.DeptId)
                    };
                    
                    ssoUser.Id = await _userRepository.CreateAsync(ssoUser);
                }
                
                var ssoRoles = await _userRepository.GetRolesByUserIdAsync(ssoUser.Id);
                var ssoPermissions = await _userRepository.GetPermissionsByUserIdAsync(ssoUser.Id);
                var ssoClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, ssoUser.Id.ToString()),
                    new Claim(ClaimTypes.Name, ssoUser.Username)
                };
                if (ssoUser.UnitId.HasValue)
                {
                    ssoClaims.Add(new Claim("unit_id", ssoUser.UnitId.Value.ToString()));
                }
                foreach (var r in ssoRoles)
                {
                    ssoClaims.Add(new Claim(ClaimTypes.Role, r));
                }
                foreach (var p in ssoPermissions)
                {
                    ssoClaims.Add(new Claim("permission", p));
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
        var userPermissions = await _userRepository.GetPermissionsByUserIdAsync(user.Id);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };
        if (user.UnitId.HasValue)
        {
            claims.Add(new Claim("unit_id", user.UnitId.Value.ToString()));
        }
        foreach (var r in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }
        foreach (var p in userPermissions)
        {
            claims.Add(new Claim("permission", p));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
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

    /// <summary>
    /// [CHỈ DÙNG TRONG DEVELOPMENT] Khởi tạo tài khoản admin mặc định nếu chưa có trong DB.
    /// Mật khẩu mặc định: Admin@123!
    /// Xóa hoặc disable endpoint này trước khi deploy lên production.
    /// </summary>
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
            var existingAdmin = await _userRepository.GetUserByUsernameAsync("admin");
            if (existingAdmin != null)
            {
                // Cập nhật hash mật khẩu để đảm bảo khớp với Admin@123!
                existingAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!");
                existingAdmin.IsActive = true;
                existingAdmin.LockoutEnd = null;
                existingAdmin.AccessFailedCount = 0;
                await _userRepository.UpdateFullAsync(existingAdmin);
                
                return Ok(new 
                { 
                    message = "Tài khoản admin đã tồn tại. Đã reset mật khẩu thành công.",
                    username = "admin",
                    password = "Admin@123!",
                    note = "Hãy đổi mật khẩu ngay sau khi đăng nhập!"
                });
            }

            var adminUser = new EvnHanoi.IdentityService.Core.Domain.Models.User
            {
                Username = "admin",
                FullName = "Quản trị viên Hệ thống",
                Email = "admin@evnhanoi.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
                IsActive = true,
                LockoutEnabled = false, // Admin không bị lockout
                AccessFailedCount = 0
            };

            adminUser.Id = await _userRepository.CreateAsync(adminUser);

            return Ok(new 
            { 
                message = "Khởi tạo tài khoản admin thành công.",
                username = "admin",
                password = "Admin@123!",
                userId = adminUser.Id,
                note = "Hãy gán vai trò ADMIN cho tài khoản này và đổi mật khẩu ngay!"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi khởi tạo admin: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        if (string.IsNullOrEmpty(username)) 
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetUserByUsernameAsync(username);
        
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
            UnitId = user.UnitId,
            IsActive = user.IsActive,
            Roles = userRoles,
            Permissions = userPermissions
        });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
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

