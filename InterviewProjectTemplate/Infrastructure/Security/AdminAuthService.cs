using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Options;
using InterviewProjectTemplate.Application.Security;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InterviewProjectTemplate.Infrastructure.Security
{
    public class AdminAuthService : IAdminAuthService
    {
        /// <summary>Role claim value that admin-only endpoints require.</summary>
        public const string AdminRole = "Admin";

        private readonly MoodTrackerDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly AdminAuthOptions _options;
        private readonly ILogger<AdminAuthService> _logger;

        public AdminAuthService(
            MoodTrackerDbContext dbContext,
            IPasswordHasher passwordHasher,
            IDateTimeProvider dateTimeProvider,
            IOptions<AdminAuthOptions> options,
            ILogger<AdminAuthService> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _dateTimeProvider = dateTimeProvider;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<AdminLoginResponse?> AuthenticateAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await _dbContext.AdminUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate => candidate.Username == request.Username,
                    cancellationToken);

            if (user is null)
            {
                // Hash anyway against a dummy value so that a request for a non-existent user takes
                // roughly the same time as one for a real user, removing the timing signal that
                // would otherwise reveal which usernames exist.
                _passwordHasher.Verify(request.Password, DummyHash);

                _logger.LogWarning(
                    "Failed admin login attempt for unknown username '{Username}'.",
                    request.Username);

                return null;
            }

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning(
                    "Failed admin login attempt for '{Username}': incorrect password.",
                    user.Username);

                return null;
            }

            var expiresAtUtc = _dateTimeProvider.UtcNow
                .AddMinutes(_options.TokenLifetimeMinutes);

            _logger.LogInformation("Admin '{Username}' signed in.", user.Username);

            return new AdminLoginResponse
            {
                AccessToken = CreateToken(user.Username, expiresAtUtc),
                ExpiresAtUtc = expiresAtUtc,
                Username = user.Username
            };
        }

        private string CreateToken(string username, DateTime expiresAtUtc)
        {
            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_options.JwtSigningKey));

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, AdminRole)
                },
                notBefore: _dateTimeProvider.UtcNow,
                expires: expiresAtUtc,
                signingCredentials: new SigningCredentials(
                    signingKey,
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// A syntactically valid hash of a value nobody knows, used only to equalise timing on the
        /// unknown-user path.
        /// </summary>
        private const string DummyHash =
            "210000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    }
}
