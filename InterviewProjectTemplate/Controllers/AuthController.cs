using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InterviewProjectTemplate.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAdminAuthService _adminAuthService;

        public AuthController(IAdminAuthService adminAuthService)
        {
            _adminAuthService = adminAuthService;
        }

        /// <summary>Exchanges admin credentials for an access token.</summary>
        /// <response code="200">Authentication succeeded.</response>
        /// <response code="400">The payload failed validation.</response>
        /// <response code="401">The credentials were rejected.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AdminLoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AdminLoginResponse>> Login(
            [FromBody] AdminLoginRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _adminAuthService.AuthenticateAsync(request, cancellationToken);

            if (result is null)
            {
                // Deliberately generic: distinguishing "no such user" from "wrong password" would
                // let an attacker enumerate valid admin usernames.
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Sign in failed",
                    Detail = "The username or password is incorrect."
                });
            }

            return Ok(result);
        }
    }
}
