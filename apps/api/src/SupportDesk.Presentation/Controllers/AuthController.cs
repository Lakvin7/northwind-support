using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Application.Features.Authentication.Login;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    [HttpGet("me")]
    public AuthenticatedAgentDto Me([FromServices] ICurrentAgent currentAgent) =>
        currentAgent.Agent;
}
