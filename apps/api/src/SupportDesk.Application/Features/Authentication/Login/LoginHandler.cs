using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Authentication.Login;

public sealed class LoginHandler(
    IAgentRepository agents,
    IPasswordHashService passwords,
    IAccessTokenService accessTokens)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email, ct);

        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash) ||
            !passwords.VerifyPassword(agent, agent.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException();
        }

        var accessToken = accessTokens.Create(agent);

        return new LoginResponse(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
