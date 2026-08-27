namespace SweeperServer.Dtos
{
    public class RankingPageResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNext { get; set; }
        public required IReadOnlyList<RankingResponse> Items { get; set; }
    }

    public class RankingResponse
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime AchievedAt { get; set; }
    }
}
