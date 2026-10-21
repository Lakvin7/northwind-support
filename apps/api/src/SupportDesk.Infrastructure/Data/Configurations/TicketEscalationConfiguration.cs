using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// Maps the escalation audit history belonging to each ticket.
/// </summary>
public sealed class TicketEscalationConfiguration
    : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", table =>
        {
            table.HasCheckConstraint(
                "CK_TicketEscalations_FromPriority",
                "[FromPriority] BETWEEN 1 AND 3");

            table.HasCheckConstraint(
                "CK_TicketEscalations_ToPriority",
                "[ToPriority] = [FromPriority] + 1");

            table.HasCheckConstraint(
                "CK_TicketEscalations_ReasonLength",
                "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 5 AND 500");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority)
            .HasConversion<int>();

        builder.Property(e => e.ToPriority)
            .HasConversion<int>();

        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .IsRequired();

        // Nullable only so escalation rows created before authentication remain readable.
        builder.Property(e => e.EscalatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.EscalatedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.PreviousDueAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.NewDueAtUtc)
            .HasColumnType("datetime2(3)");

        // Escalation history belongs to its ticket.
        // The backing field holds the records inside the Ticket aggregate.
        builder.HasOne<Ticket>()
            .WithMany(t => t.Escalations)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
