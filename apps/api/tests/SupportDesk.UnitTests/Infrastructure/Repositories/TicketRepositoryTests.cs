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
}
