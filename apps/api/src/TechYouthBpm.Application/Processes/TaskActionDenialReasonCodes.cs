namespace TechYouthBpm.Application.Processes;

public static class TaskActionDenialReasonCodes
{
    public const string TeamLeadRequired = "task.teamLeadRequired";
    public const string PermissionRequired = "task.permissionRequired";
    public const string TeamMembershipRequired = "task.teamMembershipRequired";
    public const string CommunityRoleRequired = "task.communityRoleRequired";
    public const string CommunityMismatch = "task.communityMismatch";
    public const string AssignedToAnotherUser = "task.assignedToAnotherUser";
    public const string ClaimedByAnotherUser = "task.claimedByAnotherUser";
    public const string TaskClosed = "task.closed";
}
