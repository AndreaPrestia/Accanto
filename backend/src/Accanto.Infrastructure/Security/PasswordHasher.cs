using System.Security.Cryptography;
using Accanto.Application.Common.Security;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Accanto.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password vuota.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, Iterations, HashBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;
        var parts = hash.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iters)) return false;

        byte[] salt; byte[] expected;
        try { salt = Convert.FromBase64String(parts[1]); expected = Convert.FromBase64String(parts[2]); }
        catch { return false; }

        var actual = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iters, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
