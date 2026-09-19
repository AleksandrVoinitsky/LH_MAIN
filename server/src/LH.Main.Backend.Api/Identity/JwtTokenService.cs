using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LH.Main.Contracts;
using Microsoft.IdentityModel.Tokens;

namespace LH.Main.Backend.Api.Identity;

public sealed class JwtTokenService(JwtTokenOptions options)
{
    public LoginResponse Create(Guid userId)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.Add(options.AccessTokenLifetime);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            ],
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed record JwtTokenOptions(string SigningKey, string Issuer, string Audience, TimeSpan AccessTokenLifetime);
