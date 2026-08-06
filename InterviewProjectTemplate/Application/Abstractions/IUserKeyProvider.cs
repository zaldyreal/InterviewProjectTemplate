namespace InterviewProjectTemplate.Application.Abstractions
{
    /// <summary>
    /// Supplies the anonymous identity used to enforce one mood per person per day.
    /// <para>
    /// The brief requires this without authentication, so identity is an opaque server-issued value
    /// carried in an HttpOnly cookie. That keeps it out of reach of page JavaScript, unlike a
    /// localStorage value, though it is still a browser-scoped identity rather than a real account —
    /// clearing cookies or switching browser yields a new identity. This limitation is documented in
    /// the README as an accepted consequence of the "no authentication" constraint.
    /// </para>
    /// </summary>
    public interface IUserKeyProvider
    {
        /// <summary>
        /// Returns the caller's user key, issuing and setting a new one if the request carries none.
        /// </summary>
        string GetOrCreateUserKey();
    }
}
