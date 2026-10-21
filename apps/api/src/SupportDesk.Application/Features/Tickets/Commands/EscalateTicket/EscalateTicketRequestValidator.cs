using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Validates the reason supplied when escalating a ticket.
/// </summary>
public sealed class EscalateTicketRequestValidator
    : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason =>
                reason is not null &&
                reason.Trim().Length is >= 5 and <= 500)
            .WithMessage(
                "Escalation reason must contain between 5 and 500 characters.");
    }
}
