using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.HealthChecks;

namespace SupportDesk.UnitTests.Presentation.HealthChecks;

public sealed class HealthCheckEndpointTests
{
    [Fact]
    public async Task HealthCheck_WithoutAuthentication_ReturnsHealthyWhenDatabaseIsReachable()
    {
        await using var factory = new HealthCheckApiFactory(databaseAvailable: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task HealthCheck_WithoutAuthentication_ReturnsServiceUnavailableWhenDatabaseCannotBeReached()
    {
        await using var factory = new HealthCheckApiFactory(databaseAvailable: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Unhealthy", content);
    }

    [Fact]
    public async Task HealthCheck_DoesNotExposeProtectedEndpointsWithoutToken()
    {
        await using var factory = new HealthCheckApiFactory(databaseAvailable: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenDatabaseCanConnect_ReturnsHealthy()
    {
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

        var check = new DatabaseHealthCheck(db);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenConnectionFails_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=127.0.0.1,54321;Database=Unavailable;User Id=sa;Password=Wrong;Connect Timeout=1;TrustServerCertificate=True")
            .Options;

        await using var db = new SupportDbContext(options);

        var check = new DatabaseHealthCheck(db);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private sealed class HealthCheckApiFactory : WebApplicationFactory<Program>
    {
        private const string SigningKey = "TESTING_ONLY_SIGNING_KEY_THAT_HAS_AT_LEAST_32_BYTES!";
        private const string Issuer = "NorthwindSupportDesk";
        private const string Audience = "NorthwindSupportDesk.Web";
        private readonly bool _databaseAvailable;

        public HealthCheckApiFactory(bool databaseAvailable)
        {
            _databaseAvailable = databaseAvailable;
            SetHostConfiguration();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Server=localhost;Database=UnusedHealthTests;User Id=unused;Password=unused;TrustServerCertificate=True",
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:LifetimeMinutes"] = "60",
                    ["Sla:BaseWindowHours:Low"] = "72",
                    ["Sla:BaseWindowHours:Medium"] = "24",
                    ["Sla:BaseWindowHours:High"] = "8",
                    ["Sla:BaseWindowHours:Critical"] = "4",
                    ["Sla:PremiumCustomerMultiplier"] = "0.5",
                    ["Sla:MinimumWindowHours"] = "1",
                    ["Sla:AtRiskThresholdPercent"] = "25"
                }));

            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "Database",
                        _ => new DelegateHealthCheck(_databaseAvailable),
                        null,
                        null));
                });
            });
        }

        private static void SetHostConfiguration()
        {
            var settings = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["ConnectionStrings__Default"] =
                    "Server=localhost;Database=UnusedHealthTests;User Id=unused;Password=unused;TrustServerCertificate=True",
                ["Jwt__SigningKey"] = SigningKey,
                ["Jwt__Issuer"] = Issuer,
                ["Jwt__Audience"] = Audience,
                ["Jwt__LifetimeMinutes"] = "60",
                ["Sla__BaseWindowHours__Low"] = "72",
                ["Sla__BaseWindowHours__Medium"] = "24",
                ["Sla__BaseWindowHours__High"] = "8",
                ["Sla__BaseWindowHours__Critical"] = "4",
                ["Sla__PremiumCustomerMultiplier"] = "0.5",
                ["Sla__MinimumWindowHours"] = "1",
                ["Sla__AtRiskThresholdPercent"] = "25"
            };

            foreach (var setting in settings)
            {
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            }
        }
    }

    private sealed class DelegateHealthCheck(bool isHealthy) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(isHealthy
                ? HealthCheckResult.Healthy("Database is connected.")
                : HealthCheckResult.Unhealthy("Cannot connect to database."));
        }
    }
}
