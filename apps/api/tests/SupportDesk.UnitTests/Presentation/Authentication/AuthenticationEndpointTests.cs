using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation.Authentication;

public sealed class AuthenticationEndpointTests
{
    [Fact]
    public async Task Login_InvalidCredentials_ReturnIdenticalGenericUnauthorizedResponses()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "missing@example.com", password = AuthenticationApiFactory.Password });
        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AuthenticationApiFactory.Email, password = "incorrect-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(await ReadDetailAsync(unknownEmail), await ReadDetailAsync(wrongPassword));
        Assert.Equal("The email or password is incorrect.", await ReadDetailAsync(unknownEmail));
    }

    [Fact]
    public async Task Login_InactiveAgent_ReturnsGenericUnauthorizedResponse()
    {
        await using var factory = new AuthenticationApiFactory(isActive: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AuthenticationApiFactory.Email, password = AuthenticationApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("The email or password is incorrect.", await ReadDetailAsync(response));
    }

    [Fact]
    public async Task ExistingApi_WithoutBearerToken_ReturnsUnauthorized()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_AllowsCurrentAgentRequest()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AuthenticationApiFactory.Email, password = AuthenticationApiFactory.Password });

        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal(AuthenticationApiFactory.Email, login.Agent.Email);
        Assert.Equal("Alex Turner", login.Agent.FullName);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var currentAgentResponse = await client.GetAsync("/api/auth/me");

        currentAgentResponse.EnsureSuccessStatusCode();
        var currentAgent = await currentAgentResponse.Content
            .ReadFromJsonAsync<AuthenticatedAgentDto>();
        Assert.NotNull(currentAgent);
        Assert.Equal(login.Agent, currentAgent);
    }

    [Fact]
    public async Task AuthenticationMiddleware_ExpiredToken_ReturnsUnauthorized()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthenticationApiFactory.CreateExpiredToken());

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("detail").GetString();
    }

    private sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
    {
        public const string Email = "max@northwind.local";

        public static string Password =>
            Environment.GetEnvironmentVariable("DevelopmentCredentials__AgentPassword")
            ?? "authentication-test-password";

        private const string SigningKey =
            "Integration-Test-Signing-Key-Only-Do-Not-Use-2026!";
        private const string Issuer = "NorthwindSupportDesk.Tests";
        private const string Audience = "NorthwindSupportDesk.Tests.Web";

        private readonly Mock<IAgentRepository> _agents = new();

        public AuthenticationApiFactory(bool isActive = true)
        {
            SetHostConfiguration();

            var agent = new Agent(
                "Alex Turner",
                Email,
                maxOpenTickets: 10,
                FixedClock.DefaultNow);

            if (!isActive)
            {
                agent.Deactivate();
            }

            agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));

            _agents
                .Setup(repository => repository.GetByEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string email, CancellationToken _) =>
                    string.Equals(email.Trim(), Email, StringComparison.OrdinalIgnoreCase)
                        ? agent
                        : null);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Server=localhost;Database=UnusedAuthenticationTests;User Id=unused;Password=unused;TrustServerCertificate=True",
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
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IAgentRepository>();
                services.AddSingleton(_agents.Object);
            });
        }

        public static string CreateExpiredToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, "1"),
                    new Claim(JwtRegisteredClaimNames.Name, "Alex Turner"),
                    new Claim(JwtRegisteredClaimNames.Email, Email)
                ],
                notBefore: now.AddHours(-2),
                expires: now.AddHours(-1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static void SetHostConfiguration()
        {
            var settings = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["ConnectionStrings__Default"] =
                    "Server=localhost;Database=UnusedAuthenticationTests;User Id=unused;Password=unused;TrustServerCertificate=True",
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
}
