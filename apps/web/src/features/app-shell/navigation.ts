import { Building2, ClipboardList, FilePlus2, FolderKanban, History, Inbox, LayoutDashboard, Settings, UsersRound, Workflow } from "lucide-react";
import type { TranslationKey } from "@/features/i18n/translations";
import type { PermissionName } from "@/lib/types";

export type ViewId =
  | "dashboard"
  | "forms"
  | "runner"
  | "processes"
  | "tasks"
  | "inbox"
  | "managementUsers"
  | "managementCommunities"
  | "logs"
  | "settings";

export type NavGroupId = "forms" | "management";

export type NavItem = {
  labelKey: TranslationKey;
  viewId: ViewId;
  path: string;
  icon: typeof LayoutDashboard;
  permissions?: PermissionName[];
  group?: NavGroupId;
};

export const navGroups: Record<NavGroupId, { icon: typeof LayoutDashboard; labelKey: TranslationKey }> = {
  forms: { icon: FolderKanban, labelKey: "nav.formGroup" },
  management: { icon: UsersRound, labelKey: "nav.management" },
};

export const navItems: NavItem[] = [
  {
    labelKey: "nav.dashboard",
    viewId: "dashboard",
    path: "/dashboard",
    icon: LayoutDashboard,
  },
  {
    labelKey: "nav.forms",
    viewId: "forms",
    path: "/forms",
    icon: FilePlus2,
    permissions: ["Forms.Create", "Forms.Update"],
    group: "forms",
  },
  {
    labelKey: "nav.runner",
    viewId: "runner",
    path: "/runner",
    icon: FilePlus2,
    permissions: ["Forms.View", "Processes.Start"],
    group: "forms",
  },
  {
    labelKey: "nav.processes",
    viewId: "processes",
    path: "/processes",
    icon: Workflow,
    permissions: ["Processes.View"],
  },
  {
    labelKey: "nav.tasks",
    viewId: "tasks",
    path: "/tasks",
    icon: ClipboardList,
    permissions: ["Tasks.View"],
  },
  {
    labelKey: "nav.inbox",
    viewId: "inbox",
    path: "/inbox",
    icon: Inbox,
  },
  {
    labelKey: "nav.managementUsers",
    viewId: "managementUsers",
    path: "/management/users",
    icon: UsersRound,
    permissions: ["Community.ManageUsers"],
    group: "management",
  },
  {
    labelKey: "nav.managementCommunities",
    viewId: "managementCommunities",
    path: "/management/communities",
    icon: Building2,
    permissions: ["Community.ManageRoles"],
    group: "management",
  },
  {
    labelKey: "nav.logs",
    viewId: "logs",
    path: "/logs",
    icon: History,
    permissions: ["Audit.View"],
  },
  {
    labelKey: "nav.settings",
    viewId: "settings",
    path: "/settings",
    icon: Settings,
  },
];

export function getNavItemByPath(pathname: string) {
  return navItems.find((item) => item.path === pathname || (pathname === "/" && item.viewId === "dashboard"));
}

export function getNavItemByView(viewId: ViewId) {
  return navItems.find((item) => item.viewId === viewId);
}
