using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SweeperServer.Data;
using SweeperServer.Dtos;
using SweeperServer.Models;

namespace SweeperServer.Services
{
    public class AuthService
    {
        private readonly SweeperDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokens;
        private readonly IConfiguration _configuration;

        public AuthService(SweeperDbContext db, PasswordHasher<User> passwordHasher,
            TokenService tokens, IConfiguration configuration)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _tokens = tokens;
            _configuration = configuration;
        }

        public async Task<(AuthResponse? Data, string? Error)> RegisterAsync(RegisterRequest request)
        {
            var normalizedId = NormalizeLoginId(request.LoginId);
            var nickname = request.Nickname.Trim();
            if (await _db.UserCredentials.AnyAsync(x => x.NormalizedLoginId == normalizedId))
            {
                return (null, "LOGIN_ID_ALREADY_EXISTS");
            }

            if (await _db.Users.AnyAsync(x => x.Nickname == nickname))
            {
                return (null, "NICKNAME_ALREADY_EXISTS");
            }

            var now = DateTime.UtcNow;
            var user = new User { Nickname = nickname, CreatedAt = now, UpdatedAt = now };
            user.Credential = new UserCredential
            {
                LoginId = request.LoginId.Trim(),
                NormalizedLoginId = normalizedId,
                PasswordHash = _passwordHasher.HashPassword(user, request.Password)
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return (await IssueTokensAsync(user), null);
        }

        public async Task<(AuthResponse? Data, string? Error)> LoginAsync(LoginRequest request)
        {
            var credential = await _db.UserCredentials.Include(x => x.User)
                .ThenInclude(x => x.ExternalLogins)
                .SingleOrDefaultAsync(x => x.NormalizedLoginId == NormalizeLoginId(request.LoginId));
            if (credential is null || _passwordHasher.VerifyHashedPassword(
                    credential.User, credential.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return (null, "INVALID_CREDENTIALS");
            }

            return (await IssueTokensAsync(credential.User), null);
        }

        public async Task<(AuthResponse? Data, string? Error)> GoogleLoginAsync(GoogleLoginRequest request)
        {
            var clientId = _configuration["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return (null, "GOOGLE_NOT_CONFIGURED");
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
            }
            catch (InvalidJwtException)
            {
                return (null, "INVALID_GOOGLE_TOKEN");
            }

            var login = await _db.ExternalLogins.Include(x => x.User)
                .ThenInclude(x => x.Credential)
                .SingleOrDefaultAsync(x => x.Provider == "Google" && x.ProviderUserId == payload.Subject);
            if (login is not null)
            {
                login.User.ExternalLogins = await _db.ExternalLogins.Where(x => x.UserId == login.UserId).ToListAsync();
                return (await IssueTokensAsync(login.User), null);
            }

            var nickname = await CreateAvailableNicknameAsync(request.Nickname, payload.Name, payload.Email);
            var now = DateTime.UtcNow;
            var user = new User
            {
                Nickname = nickname,
                Email = payload.Email,
                CreatedAt = now,
                UpdatedAt = now,
                ExternalLogins = [new ExternalLogin
            {
                Provider = "Google", ProviderUserId = payload.Subject,
                Email = payload.Email, CreatedAt = now
            }]
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return (await IssueTokensAsync(user), null);
        }

        public async Task<(AuthResponse? Data, string? Error)> RefreshAsync(string rawToken)
        {
            var stored = await _db.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Credential)
                .SingleOrDefaultAsync(x => x.TokenHash == TokenService.Hash(rawToken));
            if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            {
                return (null, "INVALID_REFRESH_TOKEN");
            }

            stored.User.ExternalLogins = await _db.ExternalLogins.Where(x => x.UserId == stored.UserId).ToListAsync();
            stored.RevokedAt = DateTime.UtcNow;
            return (await IssueTokensAsync(stored.User), null);
        }

        public async Task LogoutAsync(string rawToken)
        {
            var stored = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == TokenService.Hash(rawToken));
            if (stored is not null && stored.RevokedAt is null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        public async Task<UserResponse?> GetUserAsync(long userId)
        {
            var user = await _db.Users.Include(x => x.Credential).Include(x => x.ExternalLogins)
                .SingleOrDefaultAsync(x => x.Id == userId);
            return user is null ? null : ToUserResponse(user);
        }

        private async Task<AuthResponse> IssueTokensAsync(User user)
        {
            var access = _tokens.CreateAccessToken(user);
            var refresh = _tokens.CreateRefreshToken(user.Id);
            _db.RefreshTokens.Add(refresh.Entity);
            await _db.SaveChangesAsync();
            return new AuthResponse
            {
                User = ToUserResponse(user),
                AccessToken = access.Token,
                RefreshToken = refresh.RawToken,
                ExpiresIn = access.ExpiresIn
            };
        }

        private static UserResponse ToUserResponse(User user)
        {
            var providers = new List<string>();
            if (user.Credential is not null)
            {
                providers.Add("Local");
            }

            providers.AddRange(user.ExternalLogins.Select(x => x.Provider).Distinct());
            return new UserResponse
            {
                Id = user.Id,
                Nickname = user.Nickname,
                Email = user.Email,
                AuthProviders = providers
            };
        }

        private async Task<string> CreateAvailableNicknameAsync(params string?[] candidates)
        {
            var baseName = candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "Player";
            if (baseName.Contains('@'))
            {
                baseName = baseName.Split('@')[0];
            }

            baseName = baseName.Length > 20 ? baseName[..20] : baseName;
            if (baseName.Length < 2)
            {
                baseName = "Player";
            }

            var candidate = baseName;
            for (var suffix = 1; await _db.Users.AnyAsync(x => x.Nickname == candidate); suffix++)
            {
                var tail = $"_{suffix}";
                candidate = baseName[..Math.Min(baseName.Length, 20 - tail.Length)] + tail;
            }
            return candidate;
        }

        private static string NormalizeLoginId(string loginId)
        {
            return loginId.Trim().ToUpperInvariant();
        }
    }
}
