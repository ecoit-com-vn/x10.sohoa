using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
                using var httpClient = new HttpClient();
                var ssoUrl = $"http://10.9.165.18:3020/sso/serviceValidate?ticket={ticket}&appCode=QRCODE";
                
                var response = await httpClient.GetAsync(ssoUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return Unauthorized(new { message = "Không thể kết nối đến máy chủ SSO EVNHANOI để xác thực ticket." });
                }
                
                var ssoResult = await response.Content.ReadFromJsonAsync<SsoValidationResponse>();
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
                
                var ssoTokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, ssoUser.Id.ToString()),
                        new Claim(ClaimTypes.Name, ssoUser.Username)
                    }),
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
            return Unauthorized(new { message = "Invalid username or password" });
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

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            }),
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

        return Ok(new 
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            UnitId = user.UnitId,
            IsActive = user.IsActive
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

