using System.Security.Cryptography;
using System.Text;

namespace AquacultureMonitoring.Services;

public sealed class PasswordHasher
{
    public (string Hash, string Salt, int Iterations) Hash(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), iterations);
    }

    public bool Verify(string password, string expectedHash, string salt, int iterations)
    {
        try
        {
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(salt), iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(expectedHash));
        }
        catch (FormatException) { return false; }
    }
}
