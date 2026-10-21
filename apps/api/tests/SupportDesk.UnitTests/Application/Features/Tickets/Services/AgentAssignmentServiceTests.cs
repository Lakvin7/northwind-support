using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;


namespace SupportDesk.UnitTests.Application.Features.Tickets.Services;

public sealed class AgentAssignmentServiceTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ITicketRepository> _tickets = new();

    private AgentAssignmentService CreateService(
        IReadOnlyList<Agent> agents,
        IReadOnlyDictionary<int, int> counts)
    {
        _agents
            .Setup(r => r.GetActiveAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _tickets
            .Setup(r => r.GetOpenTicketCountsByAgentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        return new AgentAssignmentService(_agents.Object, _tickets.Object);
    }

    private static Agent CreateAgent(
        string name,
        int capacity = 5,
        bool specialist = false)
    {
        var agent = new Agent(
            name,
            $"{name.ToLowerInvariant()}@example.com",
            capacity,
            FixedClock.DefaultNow);

        if (specialist)
        {
            agent.AddSpecialization(3);
        }

        return agent;
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

    [Fact]
    public async Task SelectAgentAsync_WhenNoAgents_ReturnsUnassigned()
    {
        var service = CreateService(
            Array.Empty<Agent>(),
            new Dictionary<int, int>());

        var result = await service.SelectAgentAsync(
            3, false, CancellationToken.None);

        Assert.Null(result.AgentId);
        Assert.NotEmpty(result.Reason);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenAllAgentsAtCapacity_ReturnsUnassigned()
    {
        var agent = CreateAgent("Alex", capacity: 5);

        var service = CreateService(
            new[] { agent },
            new Dictionary<int, int> { [agent.Id] = 5 });

        var result = await service.SelectAgentAsync(
            3, false, CancellationToken.None);

        Assert.Null(result.AgentId);
    }

    [Fact]
    public async Task SelectAgentAsync_ChoosesLeastLoadedAgent()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var alex = CreateAgent("Alex");
        var jamie = CreateAgent("Jamie");
        var taylor = CreateAgent("Taylor");

        db.AddRange(alex, jamie, taylor);
        await db.SaveChangesAsync();

        // Deliberately supply agents in a different order.
        var service = CreateService(
            new[] { jamie, alex, taylor },
            new Dictionary<int, int>
            {
                [alex.Id] = 3,
                [jamie.Id] = 1,
                [taylor.Id] = 2
            });

        var result = await service.SelectAgentAsync(
            categoryId: 3,
            requiresSpecialist: false,
            CancellationToken.None);

        Assert.Equal(jamie.Id, result.AgentId);
        Assert.NotEmpty(result.Reason);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenWorkloadsTie_ChoosesLowestAgentId()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var alex = CreateAgent("Alex");
        var jamie = CreateAgent("Jamie");

        db.AddRange(alex, jamie);
        await db.SaveChangesAsync();

        // Supply the higher-ID agent first to verify deterministic ordering.
        var service = CreateService(
            new[] { jamie, alex },
            new Dictionary<int, int>
            {
                [alex.Id] = 2,
                [jamie.Id] = 2
            });

        var result = await service.SelectAgentAsync(
            categoryId: 3,
            requiresSpecialist: false,
            CancellationToken.None);

        Assert.True(alex.Id < jamie.Id);
        Assert.Equal(alex.Id, result.AgentId);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenSpecialistRequired_SelectsQualifiedAgent()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var category = new Category(
            "Technical Support",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        db.Add(category);
        await db.SaveChangesAsync();

        var generalAgent = CreateAgent("Alex");
        var specialist = CreateAgent("Jamie");

        specialist.AddSpecialization(category.Id);

        db.AddRange(generalAgent, specialist);
        await db.SaveChangesAsync();

        var service = CreateService(
            new[] { generalAgent, specialist },
            new Dictionary<int, int>
            {
                [generalAgent.Id] = 0,
                [specialist.Id] = 2
            });

        var result = await service.SelectAgentAsync(
            category.Id,
            requiresSpecialist: true,
            CancellationToken.None);

        Assert.Equal(specialist.Id, result.AgentId);
    }

    [Fact]
    public async Task SelectAgentAsync_WhenNoQualifiedSpecialist_ReturnsUnassigned()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var disposableConnection = connection;
        await using var disposableDb = db;

        var category = new Category(
            "Technical Support",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        db.Add(category);
        await db.SaveChangesAsync();

        var alex = CreateAgent("Alex");
        var jamie = CreateAgent("Jamie");

        db.AddRange(alex, jamie);
        await db.SaveChangesAsync();

        // Neither agent has a specialization for this category.
        var service = CreateService(
            new[] { alex, jamie },
            new Dictionary<int, int>());

        var result = await service.SelectAgentAsync(
            category.Id,
            requiresSpecialist: true,
            CancellationToken.None);

        Assert.Null(result.AgentId);
        Assert.NotEmpty(result.Reason);
    }
}
