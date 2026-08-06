using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Options;
using Microsoft.Extensions.Options;

namespace InterviewProjectTemplate.Infrastructure.Identity
{
    public class CookieUserKeyProvider : IUserKeyProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly MoodTrackerOptions _options;

        public CookieUserKeyProvider(
            IHttpContextAccessor httpContextAccessor,
            IDateTimeProvider dateTimeProvider,
            IOptions<MoodTrackerOptions> options)
        {
            _httpContextAccessor = httpContextAccessor;
            _dateTimeProvider = dateTimeProvider;
            _options = options.Value;
        }

        public string GetOrCreateUserKey()
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "An anonymous user key can only be resolved during an HTTP request.");

            var cookieName = _options.UserKeyCookieName;

            if (httpContext.Request.Cookies.TryGetValue(cookieName, out var existingKey)
                && IsWellFormed(existingKey))
            {
                return existingKey;
            }

            var userKey = Guid.NewGuid().ToString("N");

            httpContext.Response.Cookies.Append(cookieName, userKey, new CookieOptions
            {
                // Inaccessible to page JavaScript, so an XSS bug cannot read or rewrite the identity.
                HttpOnly = true,

                // Configurable because the assessment runs over plain HTTP on localhost; a TLS
                // deployment should turn this on.
                Secure = _options.UseSecureCookies,

                // The Angular app is served from a different origin (port 4200) to the API
                // (port 8080), so the cookie must be sent on cross-site XHR. SameSite=None requires
                // Secure in modern browsers, so fall back to Lax when not running under TLS.
                SameSite = _options.UseSecureCookies ? SameSiteMode.None : SameSiteMode.Lax,

                Expires = _dateTimeProvider.UtcNow.AddDays(_options.UserKeyCookieDays),
                IsEssential = true,
                Path = "/"
            });

            return userKey;
        }

        /// <summary>
        /// Rejects a cookie value that is not a bare GUID. A client can send arbitrary cookie text,
        /// and the value reaches a database column with a fixed width, so it is validated rather
        /// than trusted.
        /// </summary>
        private static bool IsWellFormed(string? value) =>
            !string.IsNullOrWhiteSpace(value) && Guid.TryParseExact(value, "N", out _);
    }
}
