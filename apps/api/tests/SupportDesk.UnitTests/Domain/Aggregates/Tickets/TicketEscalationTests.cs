using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketEscalationTests
{
    [Fact]
    public void Constructor_WithValidReason_CapturesEscalationHistory()
    {
        var escalatedAtUtc = FixedClock.DefaultNow.AddHours(3);
        var previousDueAtUtc = FixedClock.DefaultNow.AddHours(24);
        var newDueAtUtc = escalatedAtUtc.AddHours(8);

        var escalation = new TicketEscalation(
            ticketId: 41,
            fromPriority: TicketPriority.Medium,
            toPriority: TicketPriority.High,
            reason: "  Customer issue requires urgent attention.  ",
            escalatedBy: "  Alex Turner  ",
            escalatedAtUtc: escalatedAtUtc,
            previousDueAtUtc: previousDueAtUtc,
            newDueAtUtc: newDueAtUtc,
            previousAgentId: 2,
            newAgentId: 5);

        Assert.Equal(41, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(
            "Customer issue requires urgent attention.",
            escalation.Reason);
        Assert.Equal("Alex Turner", escalation.EscalatedBy);
        Assert.Equal(escalatedAtUtc, escalation.EscalatedAtUtc);
        Assert.Equal(previousDueAtUtc, escalation.PreviousDueAtUtc);
        Assert.Equal(newDueAtUtc, escalation.NewDueAtUtc);
        Assert.Equal(2, escalation.PreviousAgentId);
        Assert.Equal(5, escalation.NewAgentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    public void Constructor_WhenReasonIsTooShort_Throws(string reason)
    {
        Assert.Throws<ArgumentException>(() => CreateEscalation(reason));
    }

    [Fact]
    public void Constructor_WhenReasonExceeds500Characters_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CreateEscalation(new string('a', 501)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void Constructor_WhenReasonIsAtLengthBoundary_Succeeds(int length)
    {
        var escalation = CreateEscalation(new string('a', length));

        Assert.Equal(length, escalation.Reason.Length);
    }

    private static TicketEscalation CreateEscalation(string reason)
    {
        var nowUtc = FixedClock.DefaultNow;

        return new TicketEscalation(
            ticketId: 41,
            fromPriority: TicketPriority.Low,
            toPriority: TicketPriority.Medium,
            reason: reason,
            escalatedBy: "Alex Turner",
            escalatedAtUtc: nowUtc,
            previousDueAtUtc: nowUtc.AddHours(72),
            newDueAtUtc: nowUtc.AddHours(24),
            previousAgentId: null,
            newAgentId: 2);
    }
}
