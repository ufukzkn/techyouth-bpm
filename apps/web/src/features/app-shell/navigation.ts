import { Building2, ClipboardList, FilePlay, FilePlus2, FolderKanban, GitBranch, Handshake, History, Inbox, LayoutDashboard, Network, Settings, UsersRound, Workflow } from "lucide-react";
import type { TranslationKey } from "@/features/i18n/translations";
import type { PermissionName } from "@/lib/types";

export type ViewId =
  | "dashboard"
  | "myTeams"
  | "forms"
  | "runner"
  | "workflows"
  | "processes"
  | "tasks"
  | "inbox"
  | "managementUsers"
  | "managementCommunities"
  | "managementTeams"
  | "logs"
  | "settings";

export type NavGroupId = "design" | "processes" | "management";

export type NavItem = {
  labelKey: TranslationKey;
  viewId: ViewId;
  path: string;
  icon: typeof LayoutDashboard;
  permissions?: PermissionName[];
  group?: NavGroupId;
};

export const navGroups: Record<NavGroupId, { icon: typeof LayoutDashboard; labelKey: TranslationKey }> = {
  design: { icon: FolderKanban, labelKey: "nav.designGroup" },
  processes: { icon: Workflow, labelKey: "nav.processGroup" },
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
    labelKey: "nav.myTeams",
    viewId: "myTeams",
    path: "/teams",
    icon: Handshake,
  },
  {
    labelKey: "nav.forms",
    viewId: "forms",
    path: "/forms",
    icon: FilePlus2,
    permissions: ["Forms.Create", "Forms.Update"],
    group: "design",
  },
  {
    labelKey: "nav.runner",
    viewId: "runner",
    path: "/runner",
    icon: FilePlay,
    permissions: ["Forms.View", "Processes.Start"],
    group: "processes",
  },
  {
    labelKey: "nav.workflows",
    viewId: "workflows",
    path: "/workflows",
    icon: GitBranch,
    permissions: ["Workflows.View", "Workflows.Create", "Workflows.Update"],
    group: "design",
  },
  {
    labelKey: "nav.processes",
    viewId: "processes",
    path: "/processes",
    icon: Workflow,
    permissions: ["Processes.View"],
    group: "processes",
  },
  {
    labelKey: "nav.tasks",
    viewId: "tasks",
    path: "/tasks",
    icon: ClipboardList,
    permissions: ["Tasks.View"],
    group: "processes",
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
    labelKey: "nav.managementTeams",
    viewId: "managementTeams",
    path: "/management/teams",
    icon: Network,
    permissions: ["Teams.View"],
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
    labelKey: "nav.inbox",
    viewId: "inbox",
    path: "/inbox",
    icon: Inbox,
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
