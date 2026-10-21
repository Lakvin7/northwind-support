using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    SlaPolicy slaPolicy) : ITicketQueries
{
    /// <remarks>
    /// Applies server-side filters before counting, sorting and paging tickets.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var thresholdPercent = slaPolicy.AtRiskThresholdPercent;

        var tickets = ApplyFilters(
            TicketsWithLabels(),
            query,
            now,
            thresholdPercent,
            db.Database.IsSqlServer());

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.SlaStartedAtUtc,
                    slaPolicy.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.SlaStartedAtUtc,
                    slaPolicy.AtRiskThresholdPercent)
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns a ticket's escalation history, oldest first.
    /// Returns null when the ticket does not exist.
    /// </summary>
    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(
        int ticketId,
        CancellationToken ct)
    {
        var ticketExists = await db.Set<Ticket>()
            .AsNoTracking()
            .AnyAsync(t => t.Id == ticketId, ct);

        if (!ticketExists)
        {
            return null;
        }

        return await db.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderBy(e => e.EscalatedAtUtc)
            .ThenBy(e => e.Id)
            .Select(e => new TicketEscalationDto(
                e.Id,
                e.TicketId,
                e.FromPriority,
                e.ToPriority,
                e.Reason,
                e.EscalatedAtUtc,
                e.PreviousDueAtUtc,
                e.NewDueAtUtc,
                e.PreviousAgentId,
                e.NewAgentId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.SlaStartedAtUtc,
                    slaPolicy.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent,

            // An escalation resets the SLA window.
            // Otherwise, the window starts when the ticket was created.
            SlaStartedAtUtc = db.Set<TicketEscalation>()
                .Where(e => e.TicketId == ticket.Id)
                .Max(e => (DateTime?)e.EscalatedAtUtc)
                ?? ticket.CreatedAtUtc
        };

    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime now,
        double thresholdPercent,
        bool isSqlServer)
    {
        if (query.Status.HasValue)
            source = source.Where(x => x.Ticket.Status == query.Status.Value);

        if (query.Priority.HasValue)
            source = source.Where(x => x.Ticket.Priority == query.Priority.Value);

        if (query.SlaStatus.HasValue)
        {
            source = query.SlaStatus.Value switch
            {
                SlaStatus.NotApplicable => source.Where(x =>
                    x.Ticket.DueAtUtc == null),

                SlaStatus.Met => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc != null &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

                SlaStatus.Breached => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    ((x.Ticket.ResolvedAtUtc != null &&
                      x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                     (x.Ticket.ResolvedAtUtc == null &&
                      x.Ticket.DueAtUtc < now))),

                SlaStatus.AtRisk when isSqlServer => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    x.SlaStartedAtUtc != null &&
                    x.Ticket.DueAtUtc > x.SlaStartedAtUtc &&
                    EF.Functions.DateDiffSecond(
                        now,
                        x.Ticket.DueAtUtc.Value) * 100.0
                    <= EF.Functions.DateDiffSecond(
                        x.SlaStartedAtUtc.Value,
                        x.Ticket.DueAtUtc.Value) * thresholdPercent),

                SlaStatus.AtRisk => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    x.SlaStartedAtUtc != null &&
                    x.Ticket.DueAtUtc > x.SlaStartedAtUtc &&
                    Math.Round(
                        (x.Ticket.DueAtUtc.Value.Ticks - now.Ticks)
                        / 10_000_000.0
                    ) * 100.0
                    <=
                    Math.Round(
                        (x.Ticket.DueAtUtc.Value.Ticks -
                        x.SlaStartedAtUtc.Value.Ticks)
                        / 10_000_000.0
                    ) * thresholdPercent),

                SlaStatus.WithinSla when isSqlServer => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    (
                        x.SlaStartedAtUtc == null ||
                        x.Ticket.DueAtUtc <= x.SlaStartedAtUtc ||
                        EF.Functions.DateDiffSecond(
                            now,
                            x.Ticket.DueAtUtc.Value) * 100.0
                        > EF.Functions.DateDiffSecond(
                            x.SlaStartedAtUtc.Value,
                            x.Ticket.DueAtUtc.Value) * thresholdPercent
                    )),

                SlaStatus.WithinSla => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc == null &&
                    x.Ticket.DueAtUtc >= now &&
                    (
                        x.SlaStartedAtUtc == null ||
                        x.Ticket.DueAtUtc <= x.SlaStartedAtUtc ||
                        Math.Round(
                            (x.Ticket.DueAtUtc.Value.Ticks - now.Ticks)
                            / 10_000_000.0
                        ) * 100.0
                        >
                        Math.Round(
                            (x.Ticket.DueAtUtc.Value.Ticks -
                            x.SlaStartedAtUtc.Value.Ticks)
                            / 10_000_000.0
                        ) * thresholdPercent
                    )),

                _ => source
            };
        }

        if (query.CategoryId.HasValue)
            source = source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);

        if (query.CustomerId.HasValue)
            source = source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);

        if (query.AssignedAgentId.HasValue)
            source = source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);

        if (query.UnassignedOnly)
            source = source.Where(x => x.Ticket.AssignedAgentId == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();

            source = source.Where(x =>
                x.Ticket.Title.ToLower().Contains(search) ||
                x.Ticket.Reference.ToLower().Contains(search) ||
                x.Customer.Name.ToLower().Contains(search));
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }

        /// <summary>
        /// Start of the current SLA window, including resets after escalation.
        /// </summary>
        public DateTime? SlaStartedAtUtc { get; init; }
    }
}
