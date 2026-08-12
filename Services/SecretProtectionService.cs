using Microsoft.AspNetCore.DataProtection;

namespace AquacultureMonitoring.Services;

/// <summary>Encrypts runtime connection secrets at rest with the persisted AquaOps key ring.</summary>
public sealed class SecretProtectionService
{
    private readonly IDataProtector _protector;

    public SecretProtectionService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("AquaOps.RuntimeConnectionSecrets.v1");

    public string Protect(string plaintext) => string.IsNullOrEmpty(plaintext) ? string.Empty : _protector.Protect(plaintext);

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return string.Empty;
        return _protector.Unprotect(protectedValue);
    }
}
