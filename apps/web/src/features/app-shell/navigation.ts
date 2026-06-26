import { ClipboardList, FilePlus2, LayoutDashboard, Settings, Workflow } from "lucide-react";
import type { Role } from "@/lib/types";

export type ViewId = "dashboard" | "forms" | "runner" | "processes" | "tasks" | "settings";

export type NavItem = {
  label: string;
  viewId: ViewId;
  icon: typeof LayoutDashboard;
  roles: Role[];
};

export const navItems: NavItem[] = [
  {
    label: "Dashboard",
    viewId: "dashboard",
    icon: LayoutDashboard,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Form Tasarimi",
    viewId: "forms",
    icon: FilePlus2,
    roles: ["Admin"],
  },
  {
    label: "Form Baslat",
    viewId: "runner",
    icon: FilePlus2,
    roles: ["Admin", "User"],
  },
  {
    label: "Surecler",
    viewId: "processes",
    icon: Workflow,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Islerim",
    viewId: "tasks",
    icon: ClipboardList,
    roles: ["Admin", "Approver"],
  },
  {
    label: "Ayarlar",
    viewId: "settings",
    icon: Settings,
    roles: ["Admin", "User", "Approver"],
  },
];
