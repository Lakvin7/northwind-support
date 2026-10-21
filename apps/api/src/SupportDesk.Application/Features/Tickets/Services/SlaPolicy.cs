using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Services;

public sealed class SlaPolicy
{
    private readonly SlaOptions _options;

    public SlaPolicy(SlaOptions options)
    {
        _options = options;
    }

    public DateTime CalculateDueAtUtc(
        TicketPriority priority,
        bool isPremiumCustomer,
        DateTime startedAtUtc)
    {
        if (!_options.BaseWindowHours.TryGetValue(
                priority.ToString(), out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window configured for priority '{priority}'.");
        }

        var hours = isPremiumCustomer
            ? baseHours * _options.PremiumCustomerMultiplier
            : baseHours;

        hours = Math.Max(hours, _options.MinimumWindowHours);

        return startedAtUtc.AddHours(hours);
    }
}
