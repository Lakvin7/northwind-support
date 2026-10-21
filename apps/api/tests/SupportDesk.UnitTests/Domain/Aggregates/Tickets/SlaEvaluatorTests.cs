using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Theory]
    [InlineData(63, SlaStatus.WithinSla)] // 26.25% remaining
    [InlineData(60, SlaStatus.AtRisk)]    // Exactly 25% remaining
    [InlineData(1, SlaStatus.AtRisk)]     // One minute remaining
    [InlineData(0, SlaStatus.AtRisk)]     // Exactly at deadline
    [InlineData(-1, SlaStatus.Breached)]  // One minute overdue
    public void Evaluate_OpenTicket_UsesAtRiskBoundary(
        int minutesRemaining,
        SlaStatus expected)
    {
        var slaStart = Now;
        var dueAt = slaStart.AddHours(4);
        var checkedAt = dueAt.AddMinutes(-minutesRemaining);

        var actual = SlaEvaluator.Evaluate(
            dueAt,
            null,
            checkedAt,
            slaStart,
            25);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Evaluate_ResolvedBeforeDue_RemainsMetAfterDeadline()
    {
        var slaStart = Now;
        var dueAt = slaStart.AddHours(4);
        var resolvedAt = dueAt.AddMinutes(-1);
        var checkedAt = dueAt.AddDays(1);

        var actual = SlaEvaluator.Evaluate(
            dueAt,
            resolvedAt,
            checkedAt,
            slaStart,
            25);

        Assert.Equal(SlaStatus.Met, actual);
    }
}
