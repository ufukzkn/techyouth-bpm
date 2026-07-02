import { ClipboardList, FilePlus2, LayoutDashboard, Settings, Workflow } from "lucide-react";
import type { TranslationKey } from "@/features/i18n/translations";
import type { Role } from "@/lib/types";

export type ViewId = "dashboard" | "forms" | "runner" | "processes" | "tasks" | "settings";

export type NavItem = {
  labelKey: TranslationKey;
  viewId: ViewId;
  path: string;
  icon: typeof LayoutDashboard;
  roles: Role[];
};

export const navItems: NavItem[] = [
  {
    labelKey: "nav.dashboard",
    viewId: "dashboard",
    path: "/dashboard",
    icon: LayoutDashboard,
    roles: ["Admin", "User", "Approver"],
  },
  {
    labelKey: "nav.forms",
    viewId: "forms",
    path: "/forms",
    icon: FilePlus2,
    roles: ["Admin"],
  },
  {
    labelKey: "nav.runner",
    viewId: "runner",
    path: "/runner",
    icon: FilePlus2,
    roles: ["Admin", "User"],
  },
  {
    labelKey: "nav.processes",
    viewId: "processes",
    path: "/processes",
    icon: Workflow,
    roles: ["Admin", "User", "Approver"],
  },
  {
    labelKey: "nav.tasks",
    viewId: "tasks",
    path: "/tasks",
    icon: ClipboardList,
    roles: ["Admin", "Approver"],
  },
  {
    labelKey: "nav.settings",
    viewId: "settings",
    path: "/settings",
    icon: Settings,
    roles: ["Admin", "User", "Approver"],
  },
];

export function getNavItemByPath(pathname: string) {
  return navItems.find((item) => item.path === pathname || (pathname === "/" && item.viewId === "dashboard"));
}

export function getNavItemByView(viewId: ViewId) {
  return navItems.find((item) => item.viewId === viewId);
}
