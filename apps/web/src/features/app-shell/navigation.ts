import { ClipboardList, FilePlus2, LayoutDashboard, Settings, Workflow } from "lucide-react";
import type { Role } from "@/lib/types";

export type ViewId = "dashboard" | "forms" | "runner" | "processes" | "tasks" | "settings";

export type NavItem = {
  label: string;
  viewId: ViewId;
  path: string;
  icon: typeof LayoutDashboard;
  roles: Role[];
};

export const navItems: NavItem[] = [
  {
    label: "Dashboard",
    viewId: "dashboard",
    path: "/dashboard",
    icon: LayoutDashboard,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Form Tasarimi",
    viewId: "forms",
    path: "/forms",
    icon: FilePlus2,
    roles: ["Admin"],
  },
  {
    label: "Form Baslat",
    viewId: "runner",
    path: "/runner",
    icon: FilePlus2,
    roles: ["Admin", "User"],
  },
  {
    label: "Surecler",
    viewId: "processes",
    path: "/processes",
    icon: Workflow,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Islerim",
    viewId: "tasks",
    path: "/tasks",
    icon: ClipboardList,
    roles: ["Admin", "Approver"],
  },
  {
    label: "Ayarlar",
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
