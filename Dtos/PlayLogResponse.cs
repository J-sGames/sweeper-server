namespace SweeperServer.Dtos
{
    public class PlayLogResponse
    {
        public long Id { get; set; }
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}
