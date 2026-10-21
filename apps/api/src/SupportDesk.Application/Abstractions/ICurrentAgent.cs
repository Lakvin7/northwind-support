using SupportDesk.Application.Contracts.Authentication;

namespace SupportDesk.Application.Abstractions;

public interface ICurrentAgent
{
    AuthenticatedAgentDto Agent { get; }
}
