using BPOPlatform.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// PBKDF2/SHA-256 password hashing – uses only built-in .NET 8 APIs, no extra packages.
/// 100,000 iterations, 32-byte derived key, 16-byte random salt.
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private const int Iterations = 100_000;
    private const int KeyLength = 32; // bytes
    private const int SaltLength = 16; // bytes

    public (string Hash, string Salt) HashPassword(string plainPassword)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltLength);
        var hashBytes = Pbkdf2(plainPassword, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string plainPassword, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Pbkdf2(plainPassword, saltBytes);
        var actualHash = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static byte[] Pbkdf2(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyLength);
}
