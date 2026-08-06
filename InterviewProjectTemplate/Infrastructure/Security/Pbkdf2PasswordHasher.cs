using System.Security.Cryptography;
using InterviewProjectTemplate.Application.Security;

namespace InterviewProjectTemplate.Infrastructure.Security
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 password hashing.
    /// <para>
    /// Chosen because it ships in the .NET base class library, needs no extra dependency, and is an
    /// accepted choice for password storage. Argon2id would be preferable for a greenfield
    /// production system, but it requires a third-party package; that trade-off is noted in the
    /// README. The stored format embeds the iteration count so the work factor can be raised later
    /// without invalidating existing hashes.
    /// </para>
    /// </summary>
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 210_000;
        private const char Delimiter = '.';

        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        public string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

            return string.Join(
                Delimiter,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(key));
        }

        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            var segments = storedHash.Split(Delimiter);

            if (segments.Length != 3
                || !int.TryParse(segments[0], out var iterations)
                || iterations <= 0)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(segments[1]);
                var expectedKey = Convert.FromBase64String(segments[2]);

                var actualKey = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    Algorithm,
                    expectedKey.Length);

                // Constant-time comparison: a plain byte-by-byte equality check leaks how many
                // leading bytes matched via timing.
                return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
