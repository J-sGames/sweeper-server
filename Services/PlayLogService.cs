using SweeperServer.Data;
using SweeperServer.Dtos;
using SweeperServer.Models;
using SweeperServer.Responses;

namespace SweeperServer.Services
{
    public class PlayLogService
    {
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
    }
}