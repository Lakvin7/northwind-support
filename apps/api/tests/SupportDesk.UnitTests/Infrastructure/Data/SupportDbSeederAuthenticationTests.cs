using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

public sealed class SupportDbSeederAuthenticationTests
{
    [Fact]
    public async Task SeedAsync_UpdatesExistingDevelopmentAccountWithoutChangingOtherAgents()
    {
        const string developmentEmail = "max@northwind.local";
        const string legacyDevelopmentEmail = "alex.turner@northwind-support.example";
        var developmentPassword =
            Environment.GetEnvironmentVariable("DevelopmentCredentials__AgentPassword")
            ?? "seeder-test-password";
        var clock = new FixedClock();

        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction<string, int>(
            "LEN",
            value => value?.TrimEnd(' ').Length ?? 0);
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var passwordService = new AspNetCorePasswordHashService(new PasswordHasher<Agent>());

        var developmentAgent = new Agent(
            "Alex Turner",
            legacyDevelopmentEmail,
            10,
            clock.UtcNow);
        developmentAgent.Deactivate();
        developmentAgent.SetPasswordHash(
            passwordService.HashPassword(developmentAgent, "old-demo-password"));

        var unrelatedAgent = new Agent(
            "Priya Nair",
            "priya.nair@northwind-support.example",
            8,
            clock.UtcNow);
        unrelatedAgent.SetPasswordHash(
            passwordService.HashPassword(unrelatedAgent, "unrelated-password"));
        var unrelatedHash = unrelatedAgent.PasswordHash;

        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            null,
            CustomerTier.Standard,
            clock.UtcNow);
        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(developmentAgent, unrelatedAgent, customer, category);
        await db.SaveChangesAsync();

        var developmentAgentId = developmentAgent.Id;

        db.Add(Ticket.Raise(
            "TCK-SEED-AUTH",
            "Existing seeded ticket",
            "This ticket makes the seeder use its existing-data path.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            clock.UtcNow));
        await db.SaveChangesAsync();

        var seeder = new SupportDbSeeder(
            db,
            clock,
            passwordService,
            Options.Create(new DevelopmentCredentialsOptions
            {
                AgentEmail = developmentEmail,
                LegacyAgentEmail = legacyDevelopmentEmail,
                AgentPassword = developmentPassword
            }),
            NullLogger<SupportDbSeeder>.Instance);

        await seeder.SeedAsync();

        db.ChangeTracker.Clear();
        var agents = await db.Set<Agent>().OrderBy(agent => agent.Email).ToListAsync();
        var updatedDevelopmentAgent = agents.Single(agent => agent.Email == developmentEmail);
        var preservedAgent = agents.Single(agent => agent.Email == unrelatedAgent.Email);

        Assert.Equal(developmentAgentId, updatedDevelopmentAgent.Id);
        Assert.Equal("Alex Turner", updatedDevelopmentAgent.FullName);
        Assert.True(updatedDevelopmentAgent.IsActive);
        Assert.NotNull(updatedDevelopmentAgent.PasswordHash);
        Assert.True(passwordService.VerifyPassword(
            updatedDevelopmentAgent,
            updatedDevelopmentAgent.PasswordHash,
            developmentPassword));
        Assert.False(passwordService.VerifyPassword(
            updatedDevelopmentAgent,
            updatedDevelopmentAgent.PasswordHash,
            "incorrect-password"));
        Assert.Equal(unrelatedHash, preservedAgent.PasswordHash);
        Assert.Equal(2, agents.Count);
        Assert.Single(await db.Set<Ticket>().ToListAsync());
    }
}
