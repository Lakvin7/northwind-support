using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Services;
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

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

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

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

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

    [Fact]
    public async Task GetEscalationsAsync_ExistingTicketWithoutHistory_ReturnsEmptyList()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var nowUtc = FixedClock.DefaultNow;

        var customer = new Customer(
            "Escalation Test Customer",
            "escalation@example.com",
            null,
            CustomerTier.Standard,
            nowUtc);

        var category = new Category(
            "Escalation Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var ticket = Ticket.Raise(
            "TCK-HISTORY-001",
            "History retrieval test",
            "Verify empty escalation history.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            nowUtc);

        db.Add(ticket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

        var history = await queries.GetEscalationsAsync(
            ticket.Id,
            CancellationToken.None);

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetEscalationsAsync_NonexistentTicket_ReturnsNull()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

        var history = await queries.GetEscalationsAsync(
            ticketId: 99999,
            ct: CancellationToken.None);

        Assert.Null(history);
    }

    [Fact]
    public async Task GetEscalationsAsync_MultipleEscalations_ReturnsChronologicalHistory()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "History Test Customer",
            "history@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "History Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var ticket = Ticket.Raise(
            "TCK-HISTORY-002",
            "Multiple escalation test",
            "Verify escalation history ordering.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now);

        db.Add(ticket);
        await db.SaveChangesAsync();

        var firstEscalationAt = now.AddHours(1);
        var secondEscalationAt = now.AddHours(2);

        ticket.Escalate(
            firstEscalationAt,
            firstEscalationAt.AddHours(24),
            "First escalation due to increasing urgency.",
            null);

        ticket.Escalate(
            secondEscalationAt,
            secondEscalationAt.AddHours(8),
            "Second escalation due to unresolved impact.",
            null);

        await db.SaveChangesAsync();

        var queries = new TicketQueries(
    db,
    new FixedClock(),
    new SlaPolicy(new SlaOptions
    {
        AtRiskThresholdPercent = 25
    }));

        var history = await queries.GetEscalationsAsync(
            ticket.Id,
            CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        Assert.Equal(TicketPriority.Low, history[0].FromPriority);
        Assert.Equal(TicketPriority.Medium, history[0].ToPriority);
        Assert.Equal(firstEscalationAt, history[0].EscalatedAtUtc);

        Assert.Equal(TicketPriority.Medium, history[1].FromPriority);
        Assert.Equal(TicketPriority.High, history[1].ToPriority);
        Assert.Equal(secondEscalationAt, history[1].EscalatedAtUtc);

        Assert.All(history, escalation =>
            Assert.Equal(ticket.Id, escalation.TicketId));
    }

    [Fact]
    public async Task GetPagedAsync_WhenSlaIsNotApplicable_ReturnsOnlyTicketsWithoutDeadline()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "SLA Test Customer",
            "sla@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "SLA Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var withoutSla = Ticket.Raise(
            "TCK-SLA-001",
            "Ticket without SLA",
            "This ticket has no deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now);

        var withSla = Ticket.Raise(
            "TCK-SLA-002",
            "Ticket with SLA",
            "This ticket has a deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now,
            now.AddHours(24));

        db.AddRange(withoutSla, withSla);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.NotApplicable
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(withoutSla.Id, ticket.Id);
        Assert.Equal(SlaStatus.NotApplicable, ticket.SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSlaIsMet_ReturnsOnlyTicketsResolvedOnTime()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "SLA Met Customer",
            "slamet@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "SLA Met Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var onTimeTicket = Ticket.Raise(
            "TCK-SLA-MET-001",
            "Resolved on time",
            "Ticket resolved before its SLA deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-10),
            now.AddHours(-2));

        onTimeTicket.ChangeStatus(
            TicketStatus.Resolved,
            now.AddHours(-3));

        var lateTicket = Ticket.Raise(
            "TCK-SLA-MET-002",
            "Resolved late",
            "Ticket resolved after its SLA deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-10),
            now.AddHours(-4));

        lateTicket.ChangeStatus(
            TicketStatus.Resolved,
            now.AddHours(-2));

        db.AddRange(onTimeTicket, lateTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.Met
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(onTimeTicket.Id, ticket.Id);
        Assert.Equal(SlaStatus.Met, ticket.SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSlaIsBreached_ReturnsOnlyOverdueOrLateTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "SLA Breach Customer",
            "slabreach@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "SLA Breach Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var overdue = Ticket.Raise(
            "TCK-BREACH-001",
            "Overdue unresolved ticket",
            "This ticket missed its SLA deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-10),
            now.AddHours(-2));

        var resolvedLate = Ticket.Raise(
            "TCK-BREACH-002",
            "Resolved late",
            "This ticket was resolved after its deadline.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-10),
            now.AddHours(-4));

        resolvedLate.ChangeStatus(
            TicketStatus.Resolved,
            now.AddHours(-2));

        var withinSla = Ticket.Raise(
            "TCK-BREACH-003",
            "Within SLA",
            "This ticket still has time remaining.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            now.AddHours(-1),
            now.AddHours(5));

        db.AddRange(overdue, resolvedLate, withinSla);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.Breached,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        // Both breached tickets count, even though only one fits on page 1.
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(SlaStatus.Breached, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFilteringAtRisk_ExecutesDatabaseQuery()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.AtRisk
            },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WhenAtRisk_ReturnsTicketInsideThreshold()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "SLA Risk Customer",
            "slarisk@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "SLA Risk Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        // 8-hour SLA window, with only 1 hour remaining.
        // 25% of 8 hours = 2 hours, so this ticket is AtRisk.
        var atRisk = Ticket.Raise(
            "TCK-RISK-001",
            "Approaching SLA deadline",
            "Ticket has one hour remaining.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-7),
            now.AddHours(1));

        // 8-hour SLA window, with 6 hours remaining.
        // This ticket is WithinSla, not AtRisk.
        var withinSla = Ticket.Raise(
            "TCK-RISK-002",
            "Plenty of SLA time",
            "Ticket has six hours remaining.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-2),
            now.AddHours(6));

        db.AddRange(atRisk, withinSla);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.AtRisk
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(atRisk.Id, ticket.Id);
        Assert.Equal(SlaStatus.AtRisk, ticket.SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_WhenWithinSla_ExcludesExactAtRiskBoundary()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "SLA Boundary Customer",
            "slaboundary@example.com",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "SLA Boundary Test",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        // Eight-hour window, six hours remaining: WithinSla.
        var withinSla = Ticket.Raise(
            "TCK-WITHIN-001",
            "Within SLA",
            "Ticket has six hours remaining.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-2),
            now.AddHours(6));

        // Eight-hour window, exactly two hours remaining:
        // 25% threshold reached, so this must be AtRisk.
        var atBoundary = Ticket.Raise(
            "TCK-WITHIN-002",
            "At risk boundary",
            "Ticket has exactly 25 percent remaining.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now.AddHours(-6),
            now.AddHours(2));

        db.AddRange(withinSla, atBoundary);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(
            db,
            new FixedClock(),
            new SlaPolicy(new SlaOptions
            {
                AtRiskThresholdPercent = 25
            }));

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                SlaStatus = SlaStatus.WithinSla
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal(withinSla.Id, ticket.Id);
        Assert.Equal(SlaStatus.WithinSla, ticket.SlaStatus);
    }

    private static async Task<(SqliteConnection Connection, SupportDbContext Db)>
        CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // SQLite does not provide SQL Server's LEN function.
        // Register it so the escalation reason constraint works in tests.
        connection.CreateFunction<string, int>(
            "LEN",
            value => value?.TrimEnd(' ').Length ?? 0);

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return (connection, db);
    }
}
