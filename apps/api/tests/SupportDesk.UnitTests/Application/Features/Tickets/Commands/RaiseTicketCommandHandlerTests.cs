using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        var response = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        Assert.Equal(
            "No eligible agent with available capacity was found.",
            response.TriageExplanation);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Null(created.AssignedAgentId);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(24),
            context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryForcesCritical_OverridesRequestedPriority()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Urgent Support",
                requiresSpecialist: false,
                forcesCriticalPriority: true));

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Low),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(4),
            context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForPremiumCustomer_HalvesSlaWindow()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Premium Customer",
                "premium@example.com",
                null,
                CustomerTier.Premium,
                context.Clock.UtcNow));

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.High, context.AddedTicket.Priority);
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(4),
            context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        Assert.Null(context.AddedTicket);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenEligibleAgentsExist_AssignsLeastLoadedAgent()
    {
        var context = new TicketCommandTestContext();

        var alex = new Agent(
            "Alex Turner",
            "alex@example.com",
            5,
            context.Clock.UtcNow);

        var jamie = new Agent(
            "Jamie Smith",
            "jamie@example.com",
            5,
            context.Clock.UtcNow);

        // Give the agents distinct IDs using an in-memory EF Core database.
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            "Data Source=:memory:");

        await connection.OpenAsync();

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<
            SupportDesk.Infrastructure.Data.SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDesk.Infrastructure.Data.SupportDbContext(
            options);

        await db.Database.EnsureCreatedAsync();

        db.AddRange(alex, jamie);
        await db.SaveChangesAsync();

        context.Agents
            .Setup(a => a.GetActiveAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { alex, jamie });

        context.Tickets
            .Setup(t => t.GetOpenTicketCountsByAgentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>
            {
                [alex.Id] = 3,
                [jamie.Id] = 1
            });

        var response = await context.RaiseTicket.HandleAsync(
            Request(),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(jamie.Id, context.AddedTicket.AssignedAgentId);

        Assert.Equal(
            "Assigned to the eligible agent with the fewest open tickets (1).",
            response.TriageExplanation);

        context.VerifySaved(Times.Once());
    }
}
