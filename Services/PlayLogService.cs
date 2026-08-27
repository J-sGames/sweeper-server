using Microsoft.EntityFrameworkCore;
using SweeperServer.Data;
using SweeperServer.Dtos;
using SweeperServer.Models;
using SweeperServer.Responses;

namespace SweeperServer.Services
{
    public class PlayLogService
    {
        public const int DefaultRankingPage = 1;
        public const int DefaultRankingPageSize = 10;
        public const int MaxRankingPageSize = 100;

        private readonly SweeperDbContext _db;

        public PlayLogService(SweeperDbContext db)
        {
            _db = db;
        }

        public async Task<ApiResponse<PlayLogResponse>> InsertPlayLogAsync(PlayLogRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new ApiResponse<PlayLogResponse>
                {
                    Success = false,
                    ErrorCode = "INVALID_NAME" 
                };
            }

            if (request.EndedTime < request.StartedTime)
            {
                return new ApiResponse<PlayLogResponse>
                {
                    Success = false,
                    ErrorCode = "INVALID_PLAYTIME" 
                };
            }
            
            var playLog = new PlayLog
            {
                Name = request.Name.Trim(),
                Score = request.Score,
                StartedTime = request.StartedTime,
                EndedTime = request.EndedTime
            };

            _db.PlayLogs.Add(playLog);
            await _db.SaveChangesAsync();

            return new ApiResponse<PlayLogResponse>
            {
                Success = true,
                Data = new PlayLogResponse
                {
                    Id = playLog.Id,
                    Name = playLog.Name,
                    Score = playLog.Score
                }
            };
        }

        public async Task<ApiResponse<RankingPageResponse>> GetRankingAsync(int page, int pageSize)
        {
            if (page < 1)
            {
                return new ApiResponse<RankingPageResponse>
                {
                    Success = false,
                    ErrorCode = "INVALID_RANKING_PAGE"
                };
            }

            if (pageSize < 1 || pageSize > MaxRankingPageSize)
            {
                return new ApiResponse<RankingPageResponse>
                {
                    Success = false,
                    ErrorCode = "INVALID_RANKING_PAGE_SIZE"
                };
            }

            var offset = (long)(page - 1) * pageSize;
            if (offset > int.MaxValue)
            {
                return new ApiResponse<RankingPageResponse>
                {
                    Success = false,
                    ErrorCode = "INVALID_RANKING_PAGE"
                };
            }

            var playLogs = await _db.PlayLogs
                .AsNoTracking()
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.EndedTime)
                .ThenBy(x => x.Id)
                .Skip((int)offset)
                .Take(pageSize + 1)
                .Select(x => new
                {
                    x.Name,
                    x.Score,
                    x.EndedTime
                })
                .ToListAsync();

            var hasNext = playLogs.Count > pageSize;
            var ranking = playLogs
                .Take(pageSize)
                .Select((x, index) => new RankingResponse
                {
                    Rank = (int)offset + index + 1,
                    Name = x.Name,
                    Score = x.Score,
                    AchievedAt = x.EndedTime
                })
                .ToList();

            return new ApiResponse<RankingPageResponse>
            {
                Success = true,
                Data = new RankingPageResponse
                {
                    Page = page,
                    PageSize = pageSize,
                    HasNext = hasNext,
                    Items = ranking
                }
            };
        }
    }
}
