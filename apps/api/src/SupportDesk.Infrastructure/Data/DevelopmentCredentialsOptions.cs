namespace SupportDesk.Infrastructure.Data;

public sealed class DevelopmentCredentialsOptions
{
    public const string SectionName = "DevelopmentCredentials";

    public string AgentEmail { get; set; } = string.Empty;

    public string LegacyAgentEmail { get; set; } = string.Empty;

    public string AgentPassword { get; set; } = string.Empty;
}
