using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates an open ticket by one priority level, resets its SLA deadline,
/// re-evaluates agent assignment, and records the escalation history.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentAgent currentAgent,
    GetTicketQueryHandler getTicket,
    SlaPolicy slaPolicy,
    AgentAssignmentService agentAssignment)
{
    /// <exception cref="NotFoundException">
    /// The ticket, customer, or category does not exist.
    /// </exception>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is resolved, closed, or already Critical.
    /// </exception>
    public async Task<TicketDetailDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        // Reject invalid escalation requests before doing assignment work.
        if (!ticket.IsOpen || ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} cannot be escalated.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 500)
        {
            throw new ArgumentException(
                "Escalation reason must contain between 5 and 500 characters.",
                nameof(request));
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var nowUtc = clock.UtcNow;
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);

        var newDueAtUtc = slaPolicy.CalculateDueAtUtc(
            nextPriority,
            customer.Tier == CustomerTier.Premium,
            nowUtc);

        var assignment = await agentAssignment.SelectAgentAsync(
            category.Id,
            category.RequiresSpecialist,
            ct,
            currentAgentId: ticket.AssignedAgentId);

        // Ticket.Escalate also enforces the domain rules and appends history.
        ticket.Escalate(
            nowUtc,
            newDueAtUtc,
            request.Reason,
            currentAgent.Agent.FullName,
            assignment.AgentId);

        // The ticket update and new history record are saved together.
        await unitOfWork.SaveChangesAsync(ct);

        var result = await getTicket.HandleAsync(id, ct);
        result.TriageExplanation = assignment.Reason;

        return result;
    }
}
