using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Raise_WithSlaDeadline_StoresDueAtUtc()
    {
        var createdAt = new DateTime(
            2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);

        var dueAt = createdAt.AddHours(4);

        var ticket = Ticket.Raise(
            "TCK-0099",
            "System outage",
            "Customer reports an outage",
            1,
            2,
            TicketPriority.Critical,
            createdAt,
            dueAt);

        Assert.Equal(dueAt, ticket.DueAtUtc);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_IncreasesPriorityByExactlyOneLevel(
        TicketPriority initialPriority,
        TicketPriority expectedPriority)
    {
        var nowUtc = FixedClock.DefaultNow;
        var escalatedAtUtc = nowUtc.AddHours(3);
        var newDueAtUtc = escalatedAtUtc.AddHours(8);

        var ticket = Ticket.Raise(
            "TCK-ESC-001",
            "Escalation test",
            "Testing priority escalation.",
            1,
            3,
            initialPriority,
            nowUtc,
            nowUtc.AddHours(24));

        ticket.Escalate(
            escalatedAtUtc,
            newDueAtUtc,
            "Customer issue requires urgent attention.",
            "Alex Turner",
            newAgentId: 2);

        Assert.Equal(expectedPriority, ticket.Priority);
        Assert.Equal(newDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(escalatedAtUtc, ticket.UpdatedAtUtc);

        var history = Assert.Single(ticket.Escalations);

        Assert.Equal(initialPriority, history.FromPriority);
        Assert.Equal(expectedPriority, history.ToPriority);
        Assert.Equal(
            "Customer issue requires urgent attention.",
            history.Reason);
        Assert.Equal("Alex Turner", history.EscalatedBy);
        Assert.Equal(escalatedAtUtc, history.EscalatedAtUtc);
        Assert.Equal(nowUtc.AddHours(24), history.PreviousDueAtUtc);
        Assert.Equal(newDueAtUtc, history.NewDueAtUtc);
        Assert.Null(history.PreviousAgentId);
        Assert.Equal(2, history.NewAgentId);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(nowUtc, ticket.CreatedAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New)]
    [InlineData(TicketPriority.Medium, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]

    public void Escalate_WhenNotAllowed_ThrowsWithoutChangingTicket(
        TicketPriority priority,
        TicketStatus status)
    {
        var createdAtUtc = FixedClock.DefaultNow;
        var escalatedAtUtc = createdAtUtc.AddHours(3);
        var originalDueAtUtc = createdAtUtc.AddHours(24);

        var ticket = Ticket.Raise(
            "TCK-ESC-002",
            "Escalation rejection test",
            "Testing invalid escalation attempts.",
            1,
            3,
            priority,
            createdAtUtc,
            originalDueAtUtc);

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, createdAtUtc);
        }

        var originalUpdatedAtUtc = ticket.UpdatedAtUtc;

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(
                escalatedAtUtc,
                escalatedAtUtc.AddHours(4),
                "Customer issue required urgent attention.",
                "Alex Turner",
                newAgentId: 2));

        Assert.Equal(priority, ticket.Priority);
        Assert.Equal(status, ticket.Status);
        Assert.Equal(originalDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(originalUpdatedAtUtc, ticket.UpdatedAtUtc);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    public void Escalate_WithInvalidReason_DoesNotModifyTicket(string reason)
    {
        var nowUtc = FixedClock.DefaultNow;
        var escalatedAtUtc = nowUtc.AddHours(3);
        var originalDueAtUtc = nowUtc.AddHours(24);

        var ticket = Ticket.Raise(
            "TCK-ESC-003",
            "Invalid reason test",
            "Testing escalation reason validation.",
            1,
            3,
            TicketPriority.Medium,
            nowUtc,
            originalDueAtUtc);

        ticket.AssignTo(1, nowUtc);

        Assert.Throws<ArgumentException>(
            () => ticket.Escalate(
                escalatedAtUtc,
                escalatedAtUtc.AddHours(8),
                reason,
                "Alex Turner",
                newAgentId: 2));

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(originalDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(1, ticket.AssignedAgentId);
        Assert.Equal(nowUtc, ticket.UpdatedAtUtc);
        Assert.Empty(ticket.Escalations);
    }
}
