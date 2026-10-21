using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Services;

public sealed class SlaPolicyTests
{
    private static readonly DateTime StartedAt =
        new(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TicketPriority.Low, 72)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Critical, 4)]
    public void StandardCustomer_UsesConfiguredBaseWindow(
        TicketPriority priority,
        double expectedHours)
    {
        var policy = CreatePolicy();

        var dueAt = policy.CalculateDueAtUtc(
            priority, false, StartedAt);

        Assert.Equal(StartedAt.AddHours(expectedHours), dueAt);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 36)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Critical, 2)]
    public void PremiumCustomer_UsesHalfBaseWindow(
        TicketPriority priority,
        double expectedHours)
    {
        var policy = CreatePolicy();

        var dueAt = policy.CalculateDueAtUtc(
            priority, true, StartedAt);

        Assert.Equal(StartedAt.AddHours(expectedHours), dueAt);
    }

    [Fact]
    public void MinimumWindow_IsEnforced()
    {
        var options = CreateOptions();
        options.BaseWindowHours["Critical"] = 1;

        var policy = new SlaPolicy(options);

        var dueAt = policy.CalculateDueAtUtc(
            TicketPriority.Critical, true, StartedAt);

        Assert.Equal(StartedAt.AddHours(1), dueAt);
    }

    [Fact]
    public void MissingPriorityConfiguration_Throws()
    {
        var options = CreateOptions();
        options.BaseWindowHours.Remove("Critical");

        var policy = new SlaPolicy(options);

        Assert.Throws<InvalidOperationException>(() =>
            policy.CalculateDueAtUtc(
                TicketPriority.Critical, false, StartedAt));
    }

    private static SlaPolicy CreatePolicy() =>
        new(CreateOptions());

    private static SlaOptions CreateOptions() =>
        new()
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Low"] = 72,
                ["Medium"] = 24,
                ["High"] = 8,
                ["Critical"] = 4
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1,
            AtRiskThresholdPercent = 25
        };
}
