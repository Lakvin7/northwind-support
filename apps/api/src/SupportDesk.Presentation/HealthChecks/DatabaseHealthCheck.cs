using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.HealthChecks;

public sealed class DatabaseHealthCheck(SupportDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is connected.")
                : HealthCheckResult.Unhealthy("Cannot connect to database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
