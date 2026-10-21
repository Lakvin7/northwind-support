using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Authentication.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Authentication.Login;

public sealed class LoginHandlerTests
{
    private const string GenericFailureMessage = "The email or password is incorrect.";

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndIdentity()
    {
        var agent = CreateAgent();
        agent.SetPasswordHash("stored-hash");
        var expiry = FixedClock.DefaultNow.AddHours(1);

        var agents = new Mock<IAgentRepository>();
        agents
            .Setup(repository => repository.GetByEmailAsync(
                agent.Email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var passwords = new Mock<IPasswordHashService>();
        passwords
            .Setup(service => service.VerifyPassword(
                agent,
                "stored-hash",
                "correct-password"))
            .Returns(true);

        var accessTokens = new Mock<IAccessTokenService>();
        accessTokens
            .Setup(service => service.Create(agent))
            .Returns(new AccessTokenResult("signed-access-token", expiry));

        var handler = new LoginHandler(
            agents.Object,
            passwords.Object,
            accessTokens.Object);

        var response = await handler.HandleAsync(
            new LoginRequest(agent.Email, "correct-password"),
            CancellationToken.None);

        Assert.Equal("signed-access-token", response.AccessToken);
        Assert.Equal(expiry, response.ExpiresAtUtc);
        Assert.Equal(agent.Id, response.Agent.Id);
        Assert.Equal(agent.FullName, response.Agent.FullName);
        Assert.Equal(agent.Email, response.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownEmail_ReturnsGenericFailure()
    {
        var handler = CreateRejectedHandler(agent: null, passwordMatches: false);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => handler.HandleAsync(
                new LoginRequest("missing@example.com", "password"),
                CancellationToken.None));

        Assert.Equal(GenericFailureMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithIncorrectPassword_ReturnsGenericFailure()
    {
        var handler = CreateRejectedHandler(CreateAgent(), passwordMatches: false);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => handler.HandleAsync(
                new LoginRequest("alex.turner@northwind-support.example", "incorrect"),
                CancellationToken.None));

        Assert.Equal(GenericFailureMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveAgent_ReturnsGenericFailure()
    {
        var agent = CreateAgent();
        agent.Deactivate();
        var handler = CreateRejectedHandler(agent, passwordMatches: true);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => handler.HandleAsync(
                new LoginRequest(agent.Email, "correct-password"),
                CancellationToken.None));

        Assert.Equal(GenericFailureMessage, exception.Message);
    }

    private static LoginHandler CreateRejectedHandler(Agent? agent, bool passwordMatches)
    {
        agent?.SetPasswordHash("stored-hash");

        var agents = new Mock<IAgentRepository>();
        agents
            .Setup(repository => repository.GetByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var passwords = new Mock<IPasswordHashService>();
        passwords
            .Setup(service => service.VerifyPassword(
                It.IsAny<Agent>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(passwordMatches);

        return new LoginHandler(
            agents.Object,
            passwords.Object,
            Mock.Of<IAccessTokenService>());
    }

    private static Agent CreateAgent() =>
        new(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            maxOpenTickets: 10,
            FixedClock.DefaultNow);
}
