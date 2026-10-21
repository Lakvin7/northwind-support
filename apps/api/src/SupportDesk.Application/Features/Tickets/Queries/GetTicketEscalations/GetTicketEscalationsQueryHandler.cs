using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Returns the escalation history for a ticket, oldest first.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">
    /// No ticket exists with the requested ID.
    /// </exception>
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int id,
        CancellationToken ct)
    {
        return await tickets.GetEscalationsAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);
    }
}
