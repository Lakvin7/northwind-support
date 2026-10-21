using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IPasswordHashService
{
    string HashPassword(Agent agent, string password);

    bool VerifyPassword(Agent agent, string passwordHash, string password);
}
