using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;
using SupportDesk.Presentation.Authentication;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32 ||
    string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
    jwtOptions.LifetimeMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT configuration is missing or invalid. Configure Jwt:SigningKey, Issuer, Audience and LifetimeMinutes.");
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAgent, HttpCurrentAgent>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services
    .AddOptions<SlaOptions>()
    .Bind(builder.Configuration.GetSection(SlaOptions.SectionName))
    .Validate(options =>
        Enum.GetNames<SupportDesk.Domain.Aggregates.Tickets.TicketPriority>()
            .All(priority =>
                options.BaseWindowHours.TryGetValue(priority, out var hours)
                && double.IsFinite(hours)
                && hours > 0)
        && double.IsFinite(options.PremiumCustomerMultiplier)
        && options.PremiumCustomerMultiplier > 0
        && options.PremiumCustomerMultiplier <= 1
        && double.IsFinite(options.MinimumWindowHours)
        && options.MinimumWindowHours > 0
        && double.IsFinite(options.AtRiskThresholdPercent)
        && options.AtRiskThresholdPercent >= 0
        && options.AtRiskThresholdPercent <= 100,
        "Invalid SLA configuration. Check priority windows, multiplier, minimum window and at-risk threshold.")
    .ValidateOnStart();

builder.Services.AddScoped(sp =>
    new SlaPolicy(
        sp.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<SlaOptions>>().Value));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
