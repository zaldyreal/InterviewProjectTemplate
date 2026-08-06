using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Options;
using InterviewProjectTemplate.Domain.Entities;
using InterviewProjectTemplate.Infrastructure.Security;
using InterviewProjectTemplate.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InterviewProjectTemplate.Tests.Infrastructure
{
    public class AdminAuthServiceTests : IDisposable
    {
        private const string Username = "admin";
        private const string Password = "CorrectHorseBattery9!";

        private readonly SqliteDbContextFixture _fixture = new();
        private readonly FixedDateTimeProvider _clock = new(new DateOnly(2026, 8, 6));
        private readonly Pbkdf2PasswordHasher _hasher = new();

        private readonly AdminAuthOptions _options = new()
        {
            JwtSigningKey = "test-signing-key-that-is-long-enough-for-hmac",
            Issuer = "MoodTracker",
            Audience = "MoodTrackerAdmin",
            TokenLifetimeMinutes = 30,
            SeedUsername = Username,
            SeedPassword = Password
        };

        public AdminAuthServiceTests()
        {
            using var context = _fixture.CreateContext();

            context.AdminUsers.Add(new AdminUser
            {
                Username = Username,
                PasswordHash = _hasher.Hash(Password),
                CreatedAtUtc = _clock.UtcNow
            });

            context.SaveChanges();
        }

        public void Dispose() => _fixture.Dispose();

        private AdminAuthService CreateService()
        {
            return new AdminAuthService(
                _fixture.CreateContext(),
                _hasher,
                _clock,
                Options.Create(_options),
                NullLogger<AdminAuthService>.Instance);
        }

        [Fact]
        public async Task AuthenticateAsync_IssuesATokenForValidCredentials()
        {
            var result = await CreateService().AuthenticateAsync(
                new AdminLoginRequest { Username = Username, Password = Password });

            result.Should().NotBeNull();
            result!.Username.Should().Be(Username);
            result.AccessToken.Should().NotBeNullOrWhiteSpace();
            result.ExpiresAtUtc.Should().Be(_clock.UtcNow.AddMinutes(30));
        }

        [Fact]
        public async Task AuthenticateAsync_IssuesATokenCarryingTheAdminRole()
        {
            // The admin endpoints authorise on the role claim, so its absence would silently lock the
            // admin page even though sign-in appeared to succeed.
            var result = await CreateService().AuthenticateAsync(
                new AdminLoginRequest { Username = Username, Password = Password });

            var token = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken);

            token.Claims.Should().Contain(claim =>
                claim.Type == ClaimTypes.Role && claim.Value == AdminAuthService.AdminRole);

            token.Issuer.Should().Be(_options.Issuer);
            token.Audiences.Should().Contain(_options.Audience);
        }

        [Fact]
        public async Task AuthenticateAsync_ReturnsNullForAnIncorrectPassword()
        {
            var result = await CreateService().AuthenticateAsync(
                new AdminLoginRequest { Username = Username, Password = "not-the-password" });

            result.Should().BeNull();
        }

        [Fact]
        public async Task AuthenticateAsync_ReturnsNullForAnUnknownUsername()
        {
            var result = await CreateService().AuthenticateAsync(
                new AdminLoginRequest { Username = "someone-else", Password = Password });

            result.Should().BeNull();
        }

        [Fact]
        public async Task AuthenticateAsync_IsCaseSensitiveAboutThePassword()
        {
            var result = await CreateService().AuthenticateAsync(
                new AdminLoginRequest { Username = Username, Password = Password.ToLowerInvariant() });

            result.Should().BeNull();
        }
    }
}
