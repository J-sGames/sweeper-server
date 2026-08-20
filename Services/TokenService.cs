using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SweeperServer.Models;

namespace SweeperServer.Services
{
    public class TokenService
    {
        private readonly JwtOptions _options;
        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public (string Token, int ExpiresIn) CreateAccessToken(User user)
        {
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Nickname),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
            var jwt = new JwtSecurityToken(_options.Issuer, _options.Audience, claims,
                expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes), signingCredentials: credentials);
            return (new JwtSecurityTokenHandler().WriteToken(jwt), _options.AccessTokenMinutes * 60);
        }

        public (string RawToken, RefreshToken Entity) CreateRefreshToken(long userId)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            return (rawToken, new RefreshToken
            {
                UserId = userId,
                TokenHash = Hash(rawToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
            });
        }

        public static string Hash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
