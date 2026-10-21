using System.Text.Json.Serialization;
using SupportDesk.Application;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Infrastructure;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

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

app.MapControllers();

app.Run();
