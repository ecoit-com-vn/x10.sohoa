namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IExternalApiKeyProtector
{
    string Protect(string value);
    string? Unprotect(string? protectedValue);
}
