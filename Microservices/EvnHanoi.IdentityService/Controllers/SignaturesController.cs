using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/signatures")]
public class SignaturesController : ControllerBase
{
    [HttpPost("verify")]
    public async Task<IActionResult> VerifySignature([FromBody] VerifySignatureRequest request)
    {
        // Mock checking digital certificate status from CA
        await Task.Delay(100); // simulate some latency

        if (string.IsNullOrEmpty(request.CertificateData))
        {
            return BadRequest(new { message = "Certificate data is required" });
        }

        // Fake logic to simulate valid and invalid certificates
        // Normally this would parse the certificate, check revocation lists, expiration, and trust chain via a real CA API.
        bool isValid = !request.CertificateData.Contains("invalid");

        if (isValid)
        {
            return Ok(new
            {
                Status = "Valid",
                Message = "The digital signature and certificate are valid.",
                VerifiedAt = DateTime.UtcNow,
                Subject = "CN=Mock EVN Hanoi Staff, O=EVN HANOI, C=VN",
                Issuer = "CN=Mock CA Root, O=Mock CA Provider, C=VN"
            });
        }

        return BadRequest(new
        {
            Status = "Invalid",
            Message = "The certificate has been revoked or is invalid.",
            VerifiedAt = DateTime.UtcNow
        });
    }
}

public class VerifySignatureRequest
{
    public string CertificateData { get; set; } = string.Empty;
    public string DocumentHash { get; set; } = string.Empty;
}
