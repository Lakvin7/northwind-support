using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Retrieves active agents with their specializations for automatic assignment.
    /// </summary>
    Task<IReadOnlyList<Agent>> GetActiveAgentsAsync(CancellationToken ct);
}
