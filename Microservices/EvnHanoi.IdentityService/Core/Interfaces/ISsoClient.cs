using EvnHanoi.IdentityService.Core.DTOs;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface ISsoClient
{
    Task<SsoValidationData> ValidateTicketAsync(string ticket, CancellationToken cancellationToken = default);
}
