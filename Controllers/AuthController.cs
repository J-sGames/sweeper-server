using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SweeperServer.Dtos;
using SweeperServer.Responses;
using SweeperServer.Services;

namespace SweeperServer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;

        public AuthController(AuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request)
        {
            return ToActionResult(await _auth.RegisterAsync(request));
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request)
        {
            return ToActionResult(await _auth.LoginAsync(request));
        }

        [HttpPost("google")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Google(GoogleLoginRequest request)
        {
            return ToActionResult(await _auth.GoogleLoginAsync(request));
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshTokenRequest request)
        {
            return ToActionResult(await _auth.RefreshAsync(request.RefreshToken));
        }

        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse<object>>> Logout(LogoutRequest request)
        {
            await _auth.LogoutAsync(request.RefreshToken);
            return Ok(new ApiResponse<object> { Success = true });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> Me()
        {
            if (!long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            {
                return Unauthorized(new ApiResponse<UserResponse> { Success = false, ErrorCode = "INVALID_TOKEN" });
            }

            var user = await _auth.GetUserAsync(userId);
            return user is null
                ? NotFound(new ApiResponse<UserResponse> { Success = false, ErrorCode = "USER_NOT_FOUND" })
                : Ok(new ApiResponse<UserResponse> { Success = true, Data = user });
        }

        private ActionResult<ApiResponse<AuthResponse>> ToActionResult((AuthResponse? Data, string? Error) result)
        {
            var response = new ApiResponse<AuthResponse>
            {
                Success = result.Data is not null,
                Data = result.Data,
                ErrorCode = result.Error
            };

            return result.Error switch
            {
                null => Ok(response),
                "LOGIN_ID_ALREADY_EXISTS" or "NICKNAME_ALREADY_EXISTS" => Conflict(response),
                "GOOGLE_NOT_CONFIGURED" => StatusCode(StatusCodes.Status503ServiceUnavailable, response),
                _ => Unauthorized(response)
            };
        }
    }
}
