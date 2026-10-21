using Microsoft.AspNetCore.Identity;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class AspNetCorePasswordHashServiceTests
{
    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var agent = CreateAgent();
        var service = CreateService();
        var hash = service.HashPassword(agent, "Correct-Local-Password!2026");

        Assert.True(service.VerifyPassword(agent, hash, "Correct-Local-Password!2026"));
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        var agent = CreateAgent();
        var service = CreateService();
        var hash = service.HashPassword(agent, "Correct-Local-Password!2026");

        Assert.False(service.VerifyPassword(agent, hash, "incorrect-password"));
    }

    [Fact]
    public void VerifyPassword_WithMalformedStoredHash_ReturnsFalse()
    {
        var agent = CreateAgent();
        var service = CreateService();

        Assert.False(service.VerifyPassword(
            agent,
            "not-a-valid-password-hash",
            "Correct-Local-Password!2026"));
    }

    [Fact]
    public void HashPassword_UsesPerPasswordSalt()
    {
        var agent = CreateAgent();
        var service = CreateService();

        var first = service.HashPassword(agent, "Correct-Local-Password!2026");
        var second = service.HashPassword(agent, "Correct-Local-Password!2026");

        Assert.NotEqual(first, second);
    }

    private static Agent CreateAgent() =>
        new(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            maxOpenTickets: 10,
            FixedClock.DefaultNow);

    private static AspNetCorePasswordHashService CreateService() =>
        new(new PasswordHasher<Agent>());
}
