using System.ComponentModel.DataAnnotations;

namespace InterviewProjectTemplate.Application.Dtos
{
    public class AdminLoginRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }

    public class AdminLoginResponse
    {
        public string AccessToken { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public string Username { get; init; } = string.Empty;
    }
}