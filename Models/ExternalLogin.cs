namespace SweeperServer.Models
{
    public class ExternalLogin
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ProviderUserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public User User { get; set; } = null!;
    }
}
