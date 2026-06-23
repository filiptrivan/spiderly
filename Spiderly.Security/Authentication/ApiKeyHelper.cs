using System.Security.Cryptography;
using System.Text;

namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// Generates and hashes API keys. The framework owns the algorithm so key issuance and the verification
    /// done by <see cref="ApiKeyAuthenticationHandler"/> can never drift apart: a key is generated once,
    /// only its hash is persisted, and a presented key is matched by re-hashing it the same way.
    /// </summary>
    public static class ApiKeyHelper
    {
        /// <summary>
        /// Generates a cryptographically random 256-bit key, hex-encoded lowercase (64 characters). This is the
        /// plaintext value handed to the caller exactly once; only its <see cref="ComputeSha256Hash"/> is stored.
        /// </summary>
        /// <returns>A new 64-character lowercase-hex random key.</returns>
        public static string GenerateRandomKey()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Computes the lowercase-hex SHA-256 hash of <paramref name="input"/>. Used both when persisting a new
        /// key's hash and when verifying a presented key, so the two paths agree on the stored value.
        /// </summary>
        /// <param name="input">The plaintext key to hash.</param>
        /// <returns>The 64-character lowercase-hex SHA-256 digest.</returns>
        public static string ComputeSha256Hash(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
