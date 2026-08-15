using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.DTOs;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface ISsoAccountService
{
    Task<User> ValidateExistingAccountAsync(SsoValidationData data);
}
