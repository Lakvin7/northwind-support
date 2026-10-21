namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Derives SLA status from ticket dates and the current time.
/// SLA status is never stored.
/// </summary>
public static class SlaEvaluator
{
    // Preserve compatibility with existing callers.
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc) =>
        Evaluate(dueAtUtc, resolvedAtUtc, nowUtc, null, 25);

    /// <summary>
    /// Evaluates SLA status using the current SLA window's start time
    /// and a configurable at-risk threshold.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? slaStartedAtUtc,
        double atRiskThresholdPercent)
    {
        if (dueAtUtc is null)
            return SlaStatus.NotApplicable;

        if (resolvedAtUtc is not null)
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;

        if (nowUtc > dueAtUtc)
            return SlaStatus.Breached;

        if (slaStartedAtUtc is not null)
        {
            var window = dueAtUtc.Value - slaStartedAtUtc.Value;

            if (window > TimeSpan.Zero)
            {
                var remaining = dueAtUtc.Value - nowUtc;
                var threshold = window.TotalMinutes *
                    atRiskThresholdPercent / 100.0;

                if (remaining.TotalMinutes <= threshold)
                    return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }
}
