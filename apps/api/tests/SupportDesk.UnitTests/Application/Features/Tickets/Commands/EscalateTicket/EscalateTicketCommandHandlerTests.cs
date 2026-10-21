using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidEscalation_UpdatesPrioritySlaAndHistory()
    {
        var context = new TicketCommandTestContext();
        var nowUtc = context.Clock.UtcNow;

        var ticket = Ticket.Raise(
            "TCK-ESC-001",
            "Customer login problem",
            "Customers cannot access their accounts.",
            customerId: 1,
            categoryId: 1,
            priority: TicketPriority.Medium,
            nowUtc: nowUtc.AddHours(-3),
            dueAtUtc: nowUtc.AddHours(21));

        context.ExistingTicket(ticket);

        var result = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            new EscalateTicketRequest(
                "Customer reports that this issue is urgent."),
            CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(nowUtc.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(nowUtc, ticket.UpdatedAtUtc);

        var history = Assert.Single(ticket.Escalations);

        Assert.Equal(TicketPriority.Medium, history.FromPriority);
        Assert.Equal(TicketPriority.High, history.ToPriority);
        Assert.Equal(nowUtc.AddHours(21), history.PreviousDueAtUtc);
        Assert.Equal(nowUtc.AddHours(8), history.NewDueAtUtc);
        Assert.Equal(nowUtc, history.EscalatedAtUtc);
        Assert.Equal("Alex Turner", history.EscalatedBy);

        Assert.NotNull(result);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_PremiumCustomer_AppliesReducedSlaWindow()
    {
        var context = new TicketCommandTestContext();
        var nowUtc = context.Clock.UtcNow;

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Premium Customer",
                "premium@example.com",
                null,
                CustomerTier.Premium,
                nowUtc));

        var ticket = Ticket.Raise(
            "TCK-ESC-PREMIUM",
            "Premium escalation",
            "Customer needs urgent assistance.",
            customerId: 1,
            categoryId: 1,
            priority: TicketPriority.Medium,
            nowUtc: nowUtc.AddHours(-2),
            dueAtUtc: nowUtc.AddHours(10));

        context.ExistingTicket(ticket);

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            new EscalateTicketRequest(
                "Premium customer requires urgent assistance."),
            CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);

        // High = 8 hours; Premium multiplier = 0.5.
        Assert.Equal(nowUtc.AddHours(4), ticket.DueAtUtc);

        var history = Assert.Single(ticket.Escalations);
        Assert.Equal(nowUtc.AddHours(4), history.NewDueAtUtc);

        context.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New)]
    [InlineData(TicketPriority.Medium, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]
    public async Task HandleAsync_WhenEscalationNotAllowed_DoesNotSave(
        TicketPriority priority,
        TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var nowUtc = context.Clock.UtcNow;

        var ticket = Ticket.Raise(
            "TCK-ESC-BLOCKED",
            "Rejected escalation test",
            "Verify that invalid escalation is rejected.",
            customerId: 1,
            categoryId: 1,
            priority: priority,
            nowUtc: nowUtc.AddHours(-2),
            dueAtUtc: nowUtc.AddHours(24));

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, nowUtc);
        }

        context.ExistingTicket(ticket);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest(
                    "Customer requires urgent assistance."),
                CancellationToken.None));

        Assert.Equal(priority, ticket.Priority);
        Assert.Equal(status, ticket.Status);
        Assert.Empty(ticket.Escalations);

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_CurrentAgentAtCapacity_RetainsEligibleAgent()
    {
        var context = new TicketCommandTestContext();
        var nowUtc = context.Clock.UtcNow;

        // Persist the agent so EF Core assigns a real ID.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction<string, int>(
            "LEN",
            value => value?.TrimEnd(' ').Length ?? 0);

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agent = new Agent(
            "Alex Turner",
            "alex@example.com",
            maxOpenTickets: 1,
            nowUtc);

        db.Add(agent);
        await db.SaveChangesAsync();

        Assert.True(agent.Id > 0);

        context.Agents
            .Setup(a => a.GetActiveAgentsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });

        // The agent's one open ticket is the ticket being escalated.
        context.Tickets
            .Setup(t => t.GetOpenTicketCountsByAgentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>
            {
                [agent.Id] = 1
            });

        var ticket = Ticket.Raise(
            "TCK-ESC-CAPACITY",
            "Agent capacity test",
            "Verify current agent can retain their ticket.",
            customerId: 1,
            categoryId: 1,
            priority: TicketPriority.Medium,
            nowUtc: nowUtc.AddHours(-2),
            dueAtUtc: nowUtc.AddHours(22));

        ticket.AssignTo(agent.Id, nowUtc.AddHours(-2));
        context.ExistingTicket(ticket);

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            new EscalateTicketRequest(
                "Customer requires urgent assistance."),
            CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(agent.Id, ticket.AssignedAgentId);

        var history = Assert.Single(ticket.Escalations);

        Assert.Equal(agent.Id, history.PreviousAgentId);
        Assert.Equal(agent.Id, history.NewAgentId);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentsAvailable_UnassignsTicketAndRecordsHistory()
    {
        var context = new TicketCommandTestContext();
        var nowUtc = context.Clock.UtcNow;

        var ticket = Ticket.Raise(
            "TCK-ESC-UNASSIGNED",
            "Escalation reassignment test",
            "Verify reassignment when no agents are available.",
            customerId: 1,
            categoryId: 1,
            priority: TicketPriority.Medium,
            nowUtc: nowUtc.AddHours(-2),
            dueAtUtc: nowUtc.AddHours(22));

        ticket.AssignTo(5, nowUtc.AddHours(-2));
        context.ExistingTicket(ticket);

        // The shared context defaults to no active agents.
        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            new EscalateTicketRequest(
                "Customer requires urgent assistance."),
            CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Null(ticket.AssignedAgentId);

        var history = Assert.Single(ticket.Escalations);

        Assert.Equal(5, history.PreviousAgentId);
        Assert.Null(history.NewAgentId);

        context.VerifySaved(Times.Once());
    }
}
