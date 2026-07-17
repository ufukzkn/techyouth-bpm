namespace TechYouthBpm.Application.Audit;

public static class SystemAuditCategories
{
    public const string Identity = "identity";
    public const string Access = "access";
    public const string Forms = "forms";
    public const string Processes = "processes";
    public const string Tasks = "tasks";
    public const string Other = "other";

    public static string Resolve(string action, string entityType)
    {
        if (action.StartsWith("Task.", StringComparison.OrdinalIgnoreCase)
            || entityType.Equals("ProcessTask", StringComparison.OrdinalIgnoreCase))
        {
            return Tasks;
        }

        if (action.StartsWith("Process.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("ProcessDefinition.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("Workflow.", StringComparison.OrdinalIgnoreCase)
            || entityType.Equals("ProcessInstance", StringComparison.OrdinalIgnoreCase)
            || entityType.Equals("ProcessDefinition", StringComparison.OrdinalIgnoreCase))
        {
            return Processes;
        }

        if (action.StartsWith("FormDefinition.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("FormVersion.", StringComparison.OrdinalIgnoreCase)
            || entityType.Equals("FormDefinition", StringComparison.OrdinalIgnoreCase)
            || entityType.Equals("FormDefinitionVersion", StringComparison.OrdinalIgnoreCase))
        {
            return Forms;
        }

        if (action.StartsWith("Community.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("CommunityRole.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("Team.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("User.Access", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("User.PendingApproval", StringComparison.OrdinalIgnoreCase)
            || action.Equals("User.CreatedByAdmin", StringComparison.OrdinalIgnoreCase)
            || action.Equals("User.DeletedByAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Access;
        }

        if (action.StartsWith("Auth.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("User.Profile", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("User.Password", StringComparison.OrdinalIgnoreCase))
        {
            return Identity;
        }

        return Other;
    }

    public static bool IsKnown(string? category) => category is Identity or Access or Forms or Processes or Tasks or Other;
}
