using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A historical record of a ticket escalation.
/// </summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    string Reason,
    string? EscalatedBy,
    DateTime EscalatedAtUtc,
    DateTime? PreviousDueAtUtc,
    DateTime NewDueAtUtc,
    int? PreviousAgentId,
    int? NewAgentId);
