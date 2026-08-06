namespace InterviewProjectTemplate.Application.Security
{
    public interface IPasswordHasher
    {
        /// <summary>Produces a salted, iterated hash suitable for storage.</summary>
        string Hash(string password);

        /// <summary>
        /// Verifies a password against a stored hash. Returns false rather than throwing for a
        /// malformed stored hash, so corrupt data cannot be distinguished from a wrong password.
        /// </summary>
        bool Verify(string password, string storedHash);
    }
}
