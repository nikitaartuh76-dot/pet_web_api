using ASP_FINAL_PROJECT.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ASP_FINAL_PROJECT
{
    public class RefreshTokenService
    {
        private AppDbContext db;
        public RefreshTokenService(AppDbContext dbC)
        {
            db = dbC;
        }
        public async Task<IEnumerable<RefreshToken>?> GetActiveRefreshTokensAsync(string UserId)
        {
            return db.RefreshTokens.AsNoTracking().Where(t => t.UserId == UserId && t.UsedAt == null);
            
        }
        public async Task<IEnumerable<RefreshToken>?> GetAllUserTokensAsync(string UserId)
        {
            return db.RefreshTokens.AsNoTracking().Where( t => t.UserId == UserId);
        }
        public async Task<bool> SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken token) 
        { 
           var refToken = await db.RefreshTokens.AddAsync(refreshToken, token);
            if(refToken.State == EntityState.Added)
            {
                await db.SaveChangesAsync();
                return true;

            }
            else
            {
                return false;
            }
        }
        public string CreateAccessToken(ClaimsPrincipal principal, IConfiguration config)
        {
            var claims = principal.Claims.Where(claim =>
                claim.Type == ClaimTypes.NameIdentifier || claim.Type == ClaimTypes.Email ||
                claim.Type == ClaimTypes.Role || claim.Type == "permission");
            var key = new SymmetricSecurityKey(Convert.FromBase64String(
                config["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is not configured.")));
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = config["Jwt:Issuer"],
                Audience = config["Jwt:Audience"],
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public async Task<string> CreateRefreshTokenAsync(string userId, CancellationToken cancellationToken)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var refreshToken = new RefreshToken
            {
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
                UserId = userId,
                SessionId = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };
            await db.RefreshTokens.AddAsync(refreshToken);
            await db.SaveChangesAsync(cancellationToken);
            return rawToken;
        }

        public async Task<RefreshToken?> FindTokenAsync(string refreshToken)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
            return await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash);
        }

        public async Task MarkTokenUsedAsync(string refreshToken)
        {
            var token = await FindTokenAsync(refreshToken);
            if (token != null)
            {
                token.UsedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}
