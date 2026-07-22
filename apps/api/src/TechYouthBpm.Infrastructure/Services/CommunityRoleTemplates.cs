using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Infrastructure.Services;

internal static class CommunityRoleTemplates
{
    public const string CommunityAdmin = "community-admin";
    public const string FormDesigner = "form-designer";
    public const string ProcessStarter = "process-starter";
    public const string Approver = "approver";
    public const string StandardUser = "standard-user";
    public const string ReadOnly = "read-only";
    public const string Unassigned = "unassigned";
    public const string Custom = "custom";

    public static IReadOnlyList<RoleTemplateDto> All =>
    [
        new(
            Custom,
            "Ozel",
            "Bos baslar; izinler tek tek secilerek topluluga ozel rol olusturulur.",
            []),
        new(
            CommunityAdmin,
            "Topluluk Admin",
            "Topluluktaki kullanici, rol, form, surec, tum tasklar ve audit akisini yonetir.",
            PermissionNames.All),
        new(
            FormDesigner,
            "Form Tasarimcisi",
            "Formlari gorur, olusturur ve gunceller.",
            [PermissionNames.FormsView, PermissionNames.FormsCreate, PermissionNames.FormsUpdate]),
        new(
            ProcessStarter,
            "Surec Baslatici",
            "Formlari gorur, surec baslatir ve kendi toplulugundaki surecleri izler.",
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart, PermissionNames.WorkflowsView]),
        new(
            Approver,
            "Onay Sorumlusu",
            "Acik tasklari gorur, aksiyon alir ve surecleri izler.",
            [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        new(
            StandardUser,
            "Standart Kullanici",
            "Formlari gorur, surec baslatir, surecleri ve kendisine acik isleri izler.",
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart, PermissionNames.WorkflowsView, PermissionNames.TasksView]),
        new(
            ReadOnly,
            "Gozlemci",
            "Form, surec ve audit kayitlarini izler; degisiklik yapamaz.",
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesViewAll, PermissionNames.WorkflowsView, PermissionNames.AuditView]),
        new(
            Unassigned,
            "Atanmadi",
            "Kullanici topluluga baglidir ancak henuz is yetkisi yoktur.",
            [])
    ];

    public static IReadOnlyList<string> PermissionsFor(string templateKey) =>
        All.SingleOrDefault(template => template.Key.Equals(templateKey, StringComparison.OrdinalIgnoreCase))?.Permissions
        ?? [];
}
