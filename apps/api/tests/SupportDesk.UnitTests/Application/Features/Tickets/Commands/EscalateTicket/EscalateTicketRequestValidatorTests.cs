using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData("Urgent customer issue")]
    [InlineData("  Urgent issue  ")]
    public void ValidReason_PassesValidation(string reason)
    {
        var result = _validator.Validate(new EscalateTicketRequest(reason));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    public void InvalidReason_FailsValidation(string reason)
    {
        var result = _validator.Validate(new EscalateTicketRequest(reason));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void BoundaryLengthReason_PassesValidation(int length)
    {
        var reason = new string('a', length);

        var result = _validator.Validate(new EscalateTicketRequest(reason));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ReasonLongerThan500Characters_FailsValidation()
    {
        var reason = new string('a', 501);

        var result = _validator.Validate(new EscalateTicketRequest(reason));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NullReason_FailsValidation()
    {
        var result = _validator.Validate(new EscalateTicketRequest(null!));

        Assert.False(result.IsValid);
    }
}
