using FluentAssertions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace InterviewProjectTemplate.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly IAdminAuthService _adminAuthService = Substitute.For<IAdminAuthService>();
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _controller = new AuthController(_adminAuthService);
        }

        [Fact]
        public async Task Login_Returns200WithATokenForValidCredentials()
        {
            var request = new AdminLoginRequest { Username = "admin", Password = "good-password" };

            _adminAuthService
                .AuthenticateAsync(request, Arg.Any<CancellationToken>())
                .Returns(new AdminLoginResponse
                {
                    AccessToken = "a.b.c",
                    Username = "admin",
                    ExpiresAtUtc = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc)
                });

            var result = await _controller.Login(request, CancellationToken.None);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeOfType<AdminLoginResponse>()
                .Which.AccessToken.Should().Be("a.b.c");
        }

        [Fact]
        public async Task Login_Returns401WithoutRevealingWhetherTheUsernameExists()
        {
            var request = new AdminLoginRequest { Username = "admin", Password = "bad-password" };

            _adminAuthService
                .AuthenticateAsync(request, Arg.Any<CancellationToken>())
                .Returns((AdminLoginResponse?)null);

            var result = await _controller.Login(request, CancellationToken.None);

            var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

            var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;

            // A message naming the username, or distinguishing "unknown user" from "wrong password",
            // would let an attacker enumerate valid admin accounts.
            problem.Detail.Should().Be("The username or password is incorrect.");
        }
    }
}
