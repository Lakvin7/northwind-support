using System.IdentityModel.Tokens.Jwt;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;

namespace SupportDesk.Presentation.Authentication;

public sealed class HttpCurrentAgent(IHttpContextAccessor httpContextAccessor) : ICurrentAgent
{
    public AuthenticatedAgentDto Agent
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var idValue = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var fullName = principal?.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
            var email = principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            if (!int.TryParse(idValue, out var id) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email))
            {
                throw new UnauthorizedAccessException("Authentication is required.");
            }

            return new AuthenticatedAgentDto(id, fullName, email);
        }
    }
}
