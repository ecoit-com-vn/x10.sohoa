namespace EvnHanoi.IdentityService.Core.Options;

public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public bool Enabled { get; set; } = true;
    public bool AllowMockTicket { get; set; }
    public string AppCode { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = "https://sso.evnhanoi.vn/sso/login";
    public string LogoutUrl { get; set; } = "https://sso.evnhanoi.vn/sso/logout";
    public string ChangePasswordUrl { get; set; } = "https://sso.evnhanoi.vn/changePassword";
    public string ServiceValidateUrl { get; set; } = "http://10.9.165.18:3020/sso/serviceValidate";
    public int TimeoutSeconds { get; set; } = 15;
}
