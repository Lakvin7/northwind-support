using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

public sealed class TicketRepositoryTests
{
    [Fact]
    public async Task GetOpenTicketCountsByAgentAsync_CountsOnlyAssignedOpenTickets()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        // Support the SQL Server LEN function in SQLite tests.
        connection.CreateFunction<string, int>(
            "LEN",
            value => value?.TrimEnd(' ').Length ?? 0);
        await connection.OpenAsync();
        await using var disposableConnection = connection;

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var agent1 = new Agent(
            "Alex Turner",
            "alex@example.com",
            maxOpenTickets: 10,
            now);

        var agent2 = new Agent(
            "Jamie Smith",
            "jamie@example.com",
            maxOpenTickets: 10,
            now);

        var agent3 = new Agent(
            "Taylor Brooks",
            "taylor@example.com",
            maxOpenTickets: 10,
            now);

        db.AddRange(customer, category, agent1, agent2, agent3);
        await db.SaveChangesAsync();

        Ticket CreateTicket(string reference, TicketStatus status, int? agentId)
        {
            var ticket = Ticket.Raise(
                reference,
                "Test ticket",
                "Testing agent workload calculation.",
                customer.Id,
                category.Id,
                TicketPriority.Medium,
                now);

            if (status != TicketStatus.New)
            {
                ticket.ChangeStatus(status, now);
            }

            if (agentId is not null)
            {
                ticket.AssignTo(agentId, now);
            }

            return ticket;
        }

        db.AddRange(
            CreateTicket("TCK-WORK-001", TicketStatus.New, agent1.Id),
            CreateTicket("TCK-WORK-002", TicketStatus.Open, agent1.Id),
            CreateTicket("TCK-WORK-003", TicketStatus.InProgress, agent2.Id),
            CreateTicket("TCK-WORK-004", TicketStatus.Resolved, agent1.Id),
            CreateTicket("TCK-WORK-005", TicketStatus.Closed, agent2.Id),
            CreateTicket("TCK-WORK-006", TicketStatus.New, null));

        await db.SaveChangesAsync();

        var repository = new TicketRepository(db);

        var counts = await repository.GetOpenTicketCountsByAgentAsync(
            CancellationToken.None);

        Assert.Equal(2, counts[agent1.Id]);
        Assert.Equal(1, counts[agent2.Id]);
        Assert.Equal(2, counts.Count);
        Assert.False(counts.ContainsKey(agent3.Id));
    }

    [Fact]
    public async Task GetByIdAsync_EscalationHistorySurvivesSavingAndReloading()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");

        connection.CreateFunction<string, int>(
            "LEN",
            value => value?.TrimEnd(' ').Length ?? 0);

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var nowUtc = FixedClock.DefaultNow;
        var escalatedAtUtc = nowUtc.AddHours(3);
        var originalDueAtUtc = nowUtc.AddHours(24);
        var newDueAtUtc = escalatedAtUtc.AddHours(8);

        int ticketId;
        int newAgentId;

        // Create and save the initial ticket.
        await using (var db = new SupportDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer(
                "Persistence Test Customer",
                "persistence@example.com",
                null,
                CustomerTier.Standard,
                nowUtc);

            var category = new Category(
                "Persistence Test",
                requiresSpecialist: false,
                forcesCriticalPriority: false);

            var agent = new Agent(
                "Persistence Test Agent",
                "agent-persistence@example.com",
                maxOpenTickets: 10,
                nowUtc);

            db.AddRange(customer, category, agent);
            await db.SaveChangesAsync();

            var ticket = Ticket.Raise(
                "TCK-ESC-PERSIST",
                "Escalation persistence test",
                "Verify that escalation history is saved.",
                customer.Id,
                category.Id,
                TicketPriority.Medium,
                nowUtc,
                originalDueAtUtc);

            await new TicketRepository(db).AddAsync(
                ticket,
                CancellationToken.None);

            await db.SaveChangesAsync();

            ticketId = ticket.Id;
            newAgentId = agent.Id;
        }

        // Load the ticket, escalate it, and save both changes together.
        await using (var db = new SupportDbContext(options))
        {
            var repository = new TicketRepository(db);

            var ticket = await repository.GetByIdAsync(
                ticketId,
                CancellationToken.None);

            Assert.NotNull(ticket);
            Assert.Empty(ticket.Escalations);

            ticket.Escalate(
                escalatedAtUtc,
                newDueAtUtc,
                "Customer requires urgent attention.",
                "Alex Turner",
                newAgentId);

            await db.SaveChangesAsync();
        }

        // Use a fresh DbContext to prove the history came from the database.
        await using (var db = new SupportDbContext(options))
        {
            var repository = new TicketRepository(db);

            var ticket = await repository.GetByIdAsync(
                ticketId,
                CancellationToken.None);

            Assert.NotNull(ticket);

            Assert.Equal(TicketPriority.High, ticket.Priority);
            Assert.Equal(newDueAtUtc, ticket.DueAtUtc);
            Assert.Equal(newAgentId, ticket.AssignedAgentId);
            Assert.Equal(escalatedAtUtc, ticket.UpdatedAtUtc);

            var history = Assert.Single(ticket.Escalations);

            Assert.Equal(ticketId, history.TicketId);
            Assert.Equal(TicketPriority.Medium, history.FromPriority);
            Assert.Equal(TicketPriority.High, history.ToPriority);
            Assert.Equal(
                "Customer requires urgent attention.",
                history.Reason);
            Assert.Equal("Alex Turner", history.EscalatedBy);
            Assert.Equal(escalatedAtUtc, history.EscalatedAtUtc);
            Assert.Equal(originalDueAtUtc, history.PreviousDueAtUtc);
            Assert.Equal(newDueAtUtc, history.NewDueAtUtc);
            Assert.Null(history.PreviousAgentId);
            Assert.Equal(newAgentId, history.NewAgentId);
        }
    }
}
