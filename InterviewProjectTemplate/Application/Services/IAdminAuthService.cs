using InterviewProjectTemplate.Application.Dtos;

namespace InterviewProjectTemplate.Application.Services
{
    public interface IAdminAuthService
    {
        /// <summary>
        /// Validates admin credentials and issues an access token.
        /// Returns null for any authentication failure — unknown user and wrong password are
        /// deliberately indistinguishable to the caller so the endpoint cannot be used to enumerate
        /// valid usernames.
        /// </summary>
        Task<AdminLoginResponse?> AuthenticateAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default);
    }
}
