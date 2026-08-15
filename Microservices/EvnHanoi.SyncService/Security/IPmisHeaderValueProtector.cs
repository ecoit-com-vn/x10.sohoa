namespace EvnHanoi.SyncService.Security;

public interface IPmisHeaderValueProtector
{
    string Protect(string value);
    string? Unprotect(string? protectedValue);
}
