namespace SweeperServer.Models
{
    public class User
    {
        public long Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserCredential? Credential { get; set; }
        public ICollection<ExternalLogin> ExternalLogins { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    }
}
