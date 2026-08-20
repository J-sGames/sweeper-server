namespace SweeperServer.Models
{
    public class UserCredential
    {
        public long UserId { get; set; }
        public string LoginId { get; set; } = string.Empty;
        public string NormalizedLoginId { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}
