using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Infrastructure.Multitenancy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PropertyManagement.Infrastructure.Auth;

public class JwtService : IJwtService
{
    private readonly JwtOptions _opts;
    public JwtService(IOptions<JwtOptions> opts) => _opts = opts.Value;

    public AuthTokens Issue(string userId, string email, IEnumerable<string> roles, Guid? lawFirmId, Guid? clientId)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (lawFirmId.HasValue) claims.Add(new Claim(CurrentUser.ClaimLawFirmId, lawFirmId.Value.ToString()));
        if (clientId.HasValue) claims.Add(new Claim(CurrentUser.ClaimClientId, clientId.Value.ToString()));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_opts.Issuer, _opts.Audience, claims, now, expires, creds);
        var access = new JwtSecurityTokenHandler().WriteToken(token);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new AuthTokens(access, refresh, expires);
    }
}
