using System.ComponentModel.DataAnnotations;

namespace SweeperServer.Dtos
{
    public class RegisterRequest
    {
        [Required, RegularExpression("^[A-Za-z0-9_]{4,30}$")]
        public string LoginId { get; set; } = string.Empty;

        [Required, MinLength(10), MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        [Required, StringLength(20, MinimumLength = 2)]
        public string Nickname { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required]
        public string LoginId { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        [StringLength(20, MinimumLength = 2)]
        public string? Nickname { get; set; }
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public required UserResponse User { get; set; }
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    public class UserResponse
    {
        public long Id { get; set; }
        public required string Nickname { get; set; }
        public string? Email { get; set; }
        public required IReadOnlyCollection<string> AuthProviders { get; set; }
    }

    public enum AuthProvider
    {
        Local,
        Google
    }
}
