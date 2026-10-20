using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_WhenStatusIsFiltered_ReturnsOnlyMatchingTickets()
    {
        // Arrange: create an isolated temporary SQLite database.
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        // Create the records required by the ticket query's joins.
        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.Add(customer);
        db.Add(category);
        await db.SaveChangesAsync();

        // Create two tickets with different statuses.
        var newTicket = Ticket.Raise(
            "TCK-TEST-001",
            "New ticket",
            "A test ticket that is new.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var openTicket = Ticket.Raise(
            "TCK-TEST-002",
            "Open ticket",
            "A test ticket that is open.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-1));

        openTicket.ChangeStatus(TicketStatus.Open, now);

        db.AddRange(newTicket, openTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        // Act: request only New tickets.
        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.New
            },
            CancellationToken.None);

        // Assert: only the matching ticket is returned.
        Assert.Equal(1, result.TotalCount);
        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-001", ticket.Reference);
        Assert.Equal(TicketStatus.New, ticket.Status);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPriorityIsFiltered_ReturnsOnlyMatchingTickets()
    {
        // Arrange: create an isolated temporary SQLite database.
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var highTicket = Ticket.Raise(
            "TCK-TEST-003",
            "High priority ticket",
            "A ticket requiring urgent attention.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-2));

        var lowTicket = Ticket.Raise(
            "TCK-TEST-004",
            "Low priority ticket",
            "A ticket that can wait.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now.AddHours(-1));

        db.AddRange(highTicket, lowTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-003", ticket.Reference);
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public async Task GetPagedAsync_WhenCustomerIsFiltered_ReturnsOnlyMatchingTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var contoso = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var fabrikam = new Customer(
            "Fabrikam Inc",
            "helpdesk@fabrikam.example",
            "+1 415 555 0102",
            CustomerTier.Standard,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(contoso, fabrikam, category);
        await db.SaveChangesAsync();

        var contosoTicket = Ticket.Raise(
            "TCK-TEST-005",
            "Contoso support request",
            "A support request from Contoso.",
            contoso.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var fabrikamTicket = Ticket.Raise(
            "TCK-TEST-006",
            "Fabrikam support request",
            "A support request from Fabrikam.",
            fabrikam.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-1));

        db.AddRange(contosoTicket, fabrikamTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                CustomerId = contoso.Id
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-005", ticket.Reference);
        Assert.Equal(contoso.Id, ticket.Customer.Id);
    }

    [Fact]
    public async Task GetPagedAsync_WhenCategoryIsFiltered_ReturnsOnlyMatchingTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var general = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var technical = new Category(
            "Technical",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, general, technical);
        await db.SaveChangesAsync();

        var generalTicket = Ticket.Raise(
            "TCK-TEST-007",
            "General support request",
            "A general support request.",
            customer.Id,
            general.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var technicalTicket = Ticket.Raise(
            "TCK-TEST-008",
            "Technical support request",
            "A technical support request.",
            customer.Id,
            technical.Id,
            TicketPriority.High,
            now.AddHours(-1));

        db.AddRange(generalTicket, technicalTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                CategoryId = technical.Id
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-008", ticket.Reference);
        Assert.Equal(technical.Id, ticket.Category.Id);
    }

    [Fact]
    public async Task GetPagedAsync_WhenUnassignedOnly_ReturnsOnlyUnassignedTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var agent = new Agent(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            10,
            now.AddDays(-500));

        db.AddRange(customer, category, agent);
        await db.SaveChangesAsync();

        var unassignedTicket = Ticket.Raise(
            "TCK-TEST-009",
            "Unassigned support request",
            "A ticket awaiting assignment.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var assignedTicket = Ticket.Raise(
            "TCK-TEST-010",
            "Assigned support request",
            "A ticket already assigned to an agent.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-1));

        assignedTicket.AssignTo(agent.Id, now);

        db.AddRange(unassignedTicket, assignedTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                UnassignedOnly = true
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-009", ticket.Reference);
        Assert.Null(ticket.AssignedAgent);
    }

    [Fact]
    public async Task GetPagedAsync_WhenAssignedAgentIsFiltered_ReturnsOnlyMatchingTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var alex = new Agent(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            10,
            now.AddDays(-500));

        var sam = new Agent(
            "Sam Wilson",
            "sam.wilson@northwind-support.example",
            10,
            now.AddDays(-400));

        db.AddRange(customer, category, alex, sam);
        await db.SaveChangesAsync();

        var alexTicket = Ticket.Raise(
            "TCK-TEST-011",
            "Alex assigned ticket",
            "A support request assigned to Alex.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var samTicket = Ticket.Raise(
            "TCK-TEST-012",
            "Sam assigned ticket",
            "A support request assigned to Sam.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-1));

        alexTicket.AssignTo(alex.Id, now);
        samTicket.AssignTo(sam.Id, now);

        db.AddRange(alexTicket, samTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                AssignedAgentId = alex.Id
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-TEST-011", ticket.Reference);
        Assert.NotNull(ticket.AssignedAgent);
        Assert.Equal(alex.Id, ticket.AssignedAgent.Id);
    }

    [Theory]
    [InlineData("PRINTER")]
    [InlineData("tck-search-001")]
    [InlineData("contoso")]
    [InlineData("  printer  ")]
    public async Task GetPagedAsync_WhenSearchIsProvided_ReturnsMatchingTickets(
        string search)
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var contoso = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var fabrikam = new Customer(
            "Fabrikam Inc",
            "helpdesk@fabrikam.example",
            "+1 415 555 0102",
            CustomerTier.Standard,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(contoso, fabrikam, category);
        await db.SaveChangesAsync();

        var matchingTicket = Ticket.Raise(
            "TCK-SEARCH-001",
            "Printer connection issue",
            "Printer troubleshooting request.",
            contoso.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-2));

        var otherTicket = Ticket.Raise(
            "TCK-SEARCH-002",
            "Network outage",
            "Network connectivity request.",
            fabrikam.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-1));

        db.AddRange(matchingTicket, otherTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery { Search = search },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-SEARCH-001", ticket.Reference);
    }

    [Fact]
    public async Task GetPagedAsync_WhenMultipleFiltersAreApplied_ReturnsOnlyMatchingTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var matchingTicket = Ticket.Raise(
            "TCK-COMBINED-001",
            "Printer failure",
            "High priority printer issue.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-3));

        var wrongPriority = Ticket.Raise(
            "TCK-COMBINED-002",
            "Printer maintenance",
            "Low priority printer issue.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now.AddHours(-2));

        var wrongStatus = Ticket.Raise(
            "TCK-COMBINED-003",
            "Printer setup",
            "Already opened printer issue.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-1));

        wrongStatus.ChangeStatus(TicketStatus.Open, now);

        db.AddRange(matchingTicket, wrongPriority, wrongStatus);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.New,
                Priority = TicketPriority.High,
                CustomerId = customer.Id,
                CategoryId = category.Id,
                Search = "printer"
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-COMBINED-001", ticket.Reference);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFilteredAndPaged_ReturnsCorrectPageAndTotalCount()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            "+44 20 7946 0101",
            CustomerTier.Premium,
            now);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
        {
            var ticket = Ticket.Raise(
                $"TCK-PAGE-{i:000}",
                $"Matching ticket {i}",
                "Pagination test ticket.",
                customer.Id,
                category.Id,
                TicketPriority.High,
                now.AddMinutes(-i));

            db.Add(ticket);
        }

        var excludedTicket = Ticket.Raise(
            "TCK-PAGE-006",
            "Excluded ticket",
            "This ticket should not match the priority filter.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now);

        db.Add(excludedTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 2,
                PageSize = 2,
                SortBy = "createdAtUtc",
                SortDirection = "desc"
            },
            CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal("TCK-PAGE-003", result.Items[0].Reference);
        Assert.Equal("TCK-PAGE-004", result.Items[1].Reference);
    }

    private static async Task<(SqliteConnection Connection, SupportDbContext Db)>
        CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return (connection, db);
    }
}
