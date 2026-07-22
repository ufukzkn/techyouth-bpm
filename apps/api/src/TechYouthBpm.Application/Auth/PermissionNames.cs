namespace TechYouthBpm.Application.Auth;

public static class PermissionNames
{
    public const string CommunityManageUsers = "Community.ManageUsers";
    public const string CommunityManageRoles = "Community.ManageRoles";
    public const string CommunityManageAdmins = "Community.ManageAdmins";
    public const string TeamsView = "Teams.View";
    public const string TeamsManage = "Teams.Manage";
    public const string FormsView = "Forms.View";
    public const string FormsCreate = "Forms.Create";
    public const string FormsUpdate = "Forms.Update";
    public const string ProcessesView = "Processes.View";
    public const string ProcessesViewAll = "Processes.ViewAll";
    public const string ProcessesStart = "Processes.Start";
    public const string WorkflowsView = "Workflows.View";
    public const string WorkflowsCreate = "Workflows.Create";
    public const string WorkflowsUpdate = "Workflows.Update";
    public const string WorkflowsPublish = "Workflows.Publish";
    public const string TasksView = "Tasks.View";
    public const string TasksAct = "Tasks.Act";
    public const string TasksManageAll = "Tasks.ManageAll";
    public const string AuditView = "Audit.View";

    public static readonly IReadOnlyList<string> All =
    [
        CommunityManageUsers,
        CommunityManageRoles,
        CommunityManageAdmins,
        TeamsView,
        TeamsManage,
        FormsView,
        FormsCreate,
        FormsUpdate,
        ProcessesView,
        ProcessesViewAll,
        ProcessesStart,
        WorkflowsView,
        WorkflowsCreate,
        WorkflowsUpdate,
        WorkflowsPublish,
        TasksView,
        TasksAct,
        TasksManageAll,
        AuditView
    ];
}
