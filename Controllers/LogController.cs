using Microsoft.AspNetCore.Mvc;
using SweeperServer.Dtos;
using SweeperServer.Responses;
using SweeperServer.Services;

namespace SweeperServer.Controllers
{
    [ApiController]
    [Route("api/result")]
    public class LogController : ControllerBase
    {
        private readonly PlayLogService _playLogService;

        public LogController(PlayLogService playLogService)
        {
            _playLogService = playLogService;
        }

        [HttpPost("achieve")]
        public async Task<ActionResult<ApiResponse<PlayLogResponse>>> Login([FromBody] PlayLogRequest request)
        {
            var response = await _playLogService.InsertPlayLogAsync(request);

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        [HttpGet("ranking")]
        public async Task<ActionResult<ApiResponse<RankingPageResponse>>> GetRanking(
            [FromQuery] int page = PlayLogService.DefaultRankingPage,
            [FromQuery] int pageSize = PlayLogService.DefaultRankingPageSize)
        {
            var response = await _playLogService.GetRankingAsync(page, pageSize);

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }
    }
}
