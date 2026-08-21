namespace EvnHanoi.IdentityService.Core.Options;

public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public bool Enabled { get; set; } = true;
    public bool AllowMockTicket { get; set; }
    public string AppCode { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = "http://10.9.169.44:8081/sso/login";
    public string LogoutUrl { get; set; } = "http://10.9.169.44:8081/sso/logout";
    public string ChangePasswordUrl { get; set; } = "http://10.9.169.44:8081/changePassword";
    public string ServiceValidateUrl { get; set; } = "http://10.9.169.44:8081/serviceValidate";
    public int TimeoutSeconds { get; set; } = 15;
}
