using System.Security.Cryptography;

namespace SarahBeauty.Business.Security;

internal static class Passphrase
{
    internal const int Iterations = 600_000;

    internal static (string Salt, string Hash) Hash(string phrase)
    {
        if (phrase.Length < 15 || phrase.Length > 256)
            throw new ArgumentException("Use a passphrase with 15 to 256 characters.");
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        byte[] hash = Derive(phrase, salt, Iterations);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    internal static bool Verify(string phrase, string salt, string hash, int iterations)
    {
        if (phrase.Length > 256 || iterations < Iterations || iterations > 2_000_000) return false;
        return CryptographicOperations.FixedTimeEquals(
            Derive(phrase, Convert.FromBase64String(salt), iterations), Convert.FromBase64String(hash));
    }

    private static byte[] Derive(string phrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(phrase, salt, iterations, HashAlgorithmName.SHA256, 32);
}
