using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

public sealed class AgentRepositoryTests
{
    [Fact]
    public async Task GetActiveAgentsAsync_ExcludesInactiveAgentsAndLoadsSpecializations()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var disposableConnection = connection;

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var category = new Category(
            "Technical Support",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        db.Add(category);
        await db.SaveChangesAsync();

        var activeSpecialist = new Agent(
            "Alex Turner",
            "alex@example.com",
            5,
            FixedClock.DefaultNow);

        activeSpecialist.AddSpecialization(category.Id);

        var inactiveSpecialist = new Agent(
            "Jamie Smith",
            "jamie@example.com",
            5,
            FixedClock.DefaultNow);

        inactiveSpecialist.AddSpecialization(category.Id);
        inactiveSpecialist.Deactivate();

        db.AddRange(activeSpecialist, inactiveSpecialist);
        await db.SaveChangesAsync();

        var repository = new AgentRepository(db);

        var agents = await repository.GetActiveAgentsAsync(
            CancellationToken.None);

        var selectedAgent = Assert.Single(agents);

        Assert.Equal(activeSpecialist.Id, selectedAgent.Id);
        Assert.True(selectedAgent.IsActive);
        Assert.Contains(
            selectedAgent.Specializations,
            specialization => specialization.CategoryId == category.Id);
    }
}
