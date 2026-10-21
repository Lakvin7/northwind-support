using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    SlaPolicy slaPolicy,
    AgentAssignmentService agentAssignment,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct);
        if (customer is null)
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var category = await categories.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        // Categories can override the priority requested by the customer.
        var effectivePriority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;

        var nowUtc = clock.UtcNow;

        var dueAtUtc = slaPolicy.CalculateDueAtUtc(
            effectivePriority,
            customer.Tier == CustomerTier.Premium,
            nowUtc);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            effectivePriority,
            nowUtc,
            dueAtUtc);

        var assignment = await agentAssignment.SelectAgentAsync(
            category.Id,
            category.RequiresSpecialist,
            ct);

        if (assignment.AgentId is int agentId)
        {
            ticket.AssignTo(agentId, nowUtc);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}. " +
            "Assigned agent: {AgentId}. Assignment reason: {AssignmentReason}",
            ticket.Reference,
            ticket.CustomerId,
            ticket.CategoryId,
            assignment.AgentId,
            assignment.Reason);

        var result = await getTicket.HandleAsync(ticket.Id, ct);

        result.TriageExplanation = assignment.Reason;

        return result;
    }
}
