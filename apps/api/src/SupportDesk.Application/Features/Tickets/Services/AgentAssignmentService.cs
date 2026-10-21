using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Services;

/// <summary>
/// Selects the least-loaded eligible agent for a ticket.
/// </summary>
public sealed class AgentAssignmentService(
    IAgentRepository agents,
    ITicketRepository tickets)
{
    public async Task<AgentAssignmentResult> SelectAgentAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct,
        int? currentAgentId = null)
    {
        var activeAgents = await agents.GetActiveAgentsAsync(ct);
        var openCounts = await tickets.GetOpenTicketCountsByAgentAsync(ct);

        var selected = activeAgents
            .Where(agent =>
                !requiresSpecialist ||
                agent.Specializations.Any(s => s.CategoryId == categoryId))
            .Select(agent => new
            {
                Agent = agent,
                OpenCount = openCounts.GetValueOrDefault(agent.Id)
            })
            .Where(candidate =>
                candidate.Agent.Id == currentAgentId ||
                candidate.OpenCount < candidate.Agent.MaxOpenTickets)
            .OrderBy(candidate => candidate.OpenCount)
            .ThenBy(candidate => candidate.Agent.Id)
            .FirstOrDefault();

        if (selected is null)
        {
            return new AgentAssignmentResult(
                null,
                "No eligible agent with available capacity was found.");
        }

        return new AgentAssignmentResult(
            selected.Agent.Id,
            selected.Agent.Id == currentAgentId
                ? "Retained the current eligible agent after workload evaluation."
                : $"Assigned to the eligible agent with the fewest open tickets ({selected.OpenCount}).");
    }
}

/// <summary>
/// Records the selected agent and the reason for the assignment decision.
/// </summary>
public sealed record AgentAssignmentResult(
    int? AgentId,
    string Reason);
