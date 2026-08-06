using FluentAssertions;
using InterviewProjectTemplate.Application.Options;
using InterviewProjectTemplate.Infrastructure.Identity;
using InterviewProjectTemplate.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InterviewProjectTemplate.Tests.Infrastructure
{
    public class CookieUserKeyProviderTests
    {
        private const string CookieName = "mood_tracker_user";

        private readonly FixedDateTimeProvider _clock = new(new DateOnly(2026, 8, 6));

        private readonly MoodTrackerOptions _options = new()
        {
            UserKeyCookieName = CookieName,
            UserKeyCookieDays = 365,
            UseSecureCookies = false
        };

        private (CookieUserKeyProvider Provider, HttpContext Context) CreateProvider(
            string? existingCookieValue = null)
        {
            var httpContext = new DefaultHttpContext();

            if (existingCookieValue is not null)
            {
                httpContext.Request.Headers.Cookie = $"{CookieName}={existingCookieValue}";
            }

            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            return (
                new CookieUserKeyProvider(accessor, _clock, Options.Create(_options)),
                httpContext);
        }

        [Fact]
        public void GetOrCreateUserKey_IssuesAKeyToAFirstTimeVisitor()
        {
            var (provider, context) = CreateProvider();

            var userKey = provider.GetOrCreateUserKey();

            userKey.Should().NotBeNullOrWhiteSpace();
            Guid.TryParseExact(userKey, "N", out _).Should().BeTrue();

            context.Response.Headers.SetCookie.ToString()
                .Should().Contain($"{CookieName}={userKey}");
        }

        [Fact]
        public void GetOrCreateUserKey_MarksTheCookieHttpOnlySoPageScriptsCannotReadIt()
        {
            // The identity is what enforces the once-per-day rule; keeping it out of reach of
            // JavaScript means an XSS bug cannot read or rewrite it.
            var (provider, context) = CreateProvider();

            provider.GetOrCreateUserKey();

            context.Response.Headers.SetCookie.ToString().ToLowerInvariant()
                .Should().Contain("httponly");
        }

        [Fact]
        public void GetOrCreateUserKey_ReusesAnExistingValidCookie()
        {
            var existing = Guid.NewGuid().ToString("N");

            var (provider, context) = CreateProvider(existing);

            provider.GetOrCreateUserKey().Should().Be(existing);

            // Nothing to re-issue, so no Set-Cookie header should be written.
            context.Response.Headers.SetCookie.ToString().Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-guid")]
        [InlineData("'; DROP TABLE MoodEntries; --")]
        [InlineData("11111111-1111-1111-1111-111111111111")]
        public void GetOrCreateUserKey_ReplacesACookieValueItDidNotIssue(string tampered)
        {
            // A client can send any cookie text, and this value reaches a fixed-width database
            // column, so anything not matching the issued format is discarded rather than trusted.
            var (provider, _) = CreateProvider(tampered);

            var userKey = provider.GetOrCreateUserKey();

            userKey.Should().NotBe(tampered);
            Guid.TryParseExact(userKey, "N", out _).Should().BeTrue();
        }

        [Fact]
        public void GetOrCreateUserKey_UsesSecureAndSameSiteNoneWhenConfiguredForTls()
        {
            // The Angular client is served from a different origin to the API, so a credentialed
            // cross-site request needs SameSite=None, which browsers only honour alongside Secure.
            _options.UseSecureCookies = true;

            var (provider, context) = CreateProvider();

            provider.GetOrCreateUserKey();

            var setCookie = context.Response.Headers.SetCookie.ToString().ToLowerInvariant();

            setCookie.Should().Contain("secure");
            setCookie.Should().Contain("samesite=none");
        }

        [Fact]
        public void GetOrCreateUserKey_ThrowsOutsideAnHttpRequest()
        {
            var provider = new CookieUserKeyProvider(
                new HttpContextAccessor { HttpContext = null },
                _clock,
                Options.Create(_options));

            var act = () => provider.GetOrCreateUserKey();

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
