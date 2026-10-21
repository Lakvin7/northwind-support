using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

public sealed class AspNetCorePasswordHashService(IPasswordHasher<Agent> passwordHasher)
    : IPasswordHashService
{
    public string HashPassword(Agent agent, string password) =>
        passwordHasher.HashPassword(agent, password);

    public bool VerifyPassword(Agent agent, string passwordHash, string password)
    {
        try
        {
            return passwordHasher.VerifyHashedPassword(agent, passwordHash, password)
                is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
