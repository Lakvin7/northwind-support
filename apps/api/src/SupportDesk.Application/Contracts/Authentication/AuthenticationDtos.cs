namespace SupportDesk.Application.Contracts.Authentication;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedAgentDto(int Id, string FullName, string Email);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedAgentDto Agent);

public sealed record AccessTokenResult(string AccessToken, DateTime ExpiresAtUtc);
