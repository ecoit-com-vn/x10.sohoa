using EvnHanoi.IdentityService.Infrastructure.Services;
using EvnHanoi.IdentityService.Core.DTOs;
using System.Text.Json;

var cases = new[]
{
    (Code: "AUT-002", Status: 401, Message: "Ticket SSO không hợp lệ."),
    (Code: "AUT-005", Status: 401, Message: "Ticket SSO đã hết hạn. Vui lòng đăng nhập lại."),
    (Code: "AUT-006", Status: 403, Message: "Tài khoản chưa được cấp quyền truy cập ứng dụng.")
};

foreach (var testCase in cases)
{
    var result = SsoErrorMapper.Map(testCase.Code);
    if (result.Code != testCase.Code
        || result.StatusCode != testCase.Status
        || result.Message != testCase.Message)
    {
        throw new InvalidOperationException($"Contract test failed for {testCase.Code}.");
    }
    Console.WriteLine($"PASS {testCase.Code}: HTTP {result.StatusCode} - {result.Message}");
}

var numericIdentity = JsonSerializer.Deserialize<SsoValidationResponse>(
    """{"code":"API-000","status":"SUCCESS","data":{"identity":{"userId":123,"deptId":281,"orgId":10}}}""",
    new JsonSerializerOptions(JsonSerializerDefaults.Web));
if (numericIdentity?.Data?.Identity?.UserId != "123"
    || numericIdentity.Data.Identity.DeptId != "281"
    || numericIdentity.Data.Identity.OrgId != "10")
{
    throw new InvalidOperationException("Numeric SSO identifiers were not mapped to strings.");
}
Console.WriteLine("PASS numeric SSO identity mapping");
