using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// An immutable record of a successful ticket escalation.
/// </summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    public TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        string reason,
        DateTime escalatedAtUtc,
        DateTime? previousDueAtUtc,
        DateTime newDueAtUtc,
        int? previousAgentId,
        int? newAgentId)
    {
        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length is < 5 or > 500)
        {
            throw new ArgumentException(
                "Escalation reason must contain between 5 and 500 characters.",
                nameof(reason));
        }

        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        Reason = reason.Trim();
        EscalatedAtUtc = escalatedAtUtc;
        PreviousDueAtUtc = previousDueAtUtc;
        NewDueAtUtc = newDueAtUtc;
        PreviousAgentId = previousAgentId;
        NewAgentId = newAgentId;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }

    public DateTime? PreviousDueAtUtc { get; private set; }

    public DateTime NewDueAtUtc { get; private set; }

    public int? PreviousAgentId { get; private set; }

    public int? NewAgentId { get; private set; }
}
