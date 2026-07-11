namespace TechYouthBpm.Application.Auth;

public static class PermissionNames
{
    public const string CommunityManageUsers = "Community.ManageUsers";
    public const string CommunityManageRoles = "Community.ManageRoles";
    public const string CommunityManageAdmins = "Community.ManageAdmins";
    public const string FormsView = "Forms.View";
    public const string FormsCreate = "Forms.Create";
    public const string FormsUpdate = "Forms.Update";
    public const string ProcessesView = "Processes.View";
    public const string ProcessesStart = "Processes.Start";
    public const string TasksView = "Tasks.View";
    public const string TasksAct = "Tasks.Act";
    public const string AuditView = "Audit.View";

    public static readonly IReadOnlyList<string> All =
    [
        CommunityManageUsers,
        CommunityManageRoles,
        CommunityManageAdmins,
        FormsView,
        FormsCreate,
        FormsUpdate,
        ProcessesView,
        ProcessesStart,
        TasksView,
        TasksAct,
        AuditView
    ];
}
