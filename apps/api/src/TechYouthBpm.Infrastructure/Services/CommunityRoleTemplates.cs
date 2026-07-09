using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Infrastructure.Services;

internal static class CommunityRoleTemplates
{
    public const string CommunityAdmin = "community-admin";
    public const string FormDesigner = "form-designer";
    public const string ProcessStarter = "process-starter";
    public const string Approver = "approver";
    public const string LogisticsOperator = "logistics-operator";
    public const string ReadOnly = "read-only";

    public static IReadOnlyList<RoleTemplateDto> All =>
    [
        new(
            CommunityAdmin,
            "Topluluk Admin",
            "Topluluktaki kullanici, rol, form, surec, task ve audit akisini yonetir.",
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
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        new(
            Approver,
            "Onay Sorumlusu",
            "Acik tasklari gorur, aksiyon alir ve surecleri izler.",
            [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        new(
            LogisticsOperator,
            "Lojistik Gorevlisi",
            "Lojistik tasklarini gorur, teslimat aksiyonu alir ve surec durumunu izler.",
            [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        new(
            ReadOnly,
            "Salt Okuyucu",
            "Form, surec ve audit kayitlarini izler; degisiklik yapamaz.",
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.AuditView])
    ];

    public static IReadOnlyList<string> PermissionsFor(string templateKey) =>
        All.SingleOrDefault(template => template.Key.Equals(templateKey, StringComparison.OrdinalIgnoreCase))?.Permissions
        ?? [];
}
