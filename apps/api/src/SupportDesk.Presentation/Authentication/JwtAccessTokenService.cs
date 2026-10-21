using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Authentication;

public sealed class JwtAccessTokenService(JwtOptions options, IClock clock)
    : IAccessTokenService
{
    public AccessTokenResult Create(Agent agent)
    {
        var issuedAtUtc = clock.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(options.LifetimeMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
                new Claim(JwtRegisteredClaimNames.Email, agent.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
