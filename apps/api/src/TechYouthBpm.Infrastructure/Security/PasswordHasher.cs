using System.Security.Cryptography;

namespace TechYouthBpm.Infrastructure.Security;

internal static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2:v1";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Prefix}:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedPassword)
    {
        if (!IsHashed(storedPassword))
        {
            return false;
        }

        var parts = storedPassword.Split(':');
        if (parts.Length != 5 || !int.TryParse(parts[2], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static bool IsHashed(string password) => password.StartsWith($"{Prefix}:", StringComparison.Ordinal);
}
