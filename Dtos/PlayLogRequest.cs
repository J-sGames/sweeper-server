namespace SweeperServer.Dtos
{
    public class PlayLogRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime StartedTime { get; set; }
        public DateTime EndedTime { get; set; }
    }
}