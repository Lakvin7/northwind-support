using SupportDesk.Application.Contracts.Authentication;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IAccessTokenService
{
    AccessTokenResult Create(Agent agent);
}
