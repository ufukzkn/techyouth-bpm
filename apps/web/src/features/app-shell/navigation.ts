import { ClipboardList, FilePlus2, LayoutDashboard, Settings, Workflow } from "lucide-react";
import type { Role } from "@/lib/types";

export type NavItem = {
  label: string;
  href: string;
  icon: typeof LayoutDashboard;
  roles: Role[];
};

export const navItems: NavItem[] = [
  {
    label: "Dashboard",
    href: "#dashboard",
    icon: LayoutDashboard,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Form Tasarimi",
    href: "#forms",
    icon: FilePlus2,
    roles: ["Admin"],
  },
  {
    label: "Surecler",
    href: "#processes",
    icon: Workflow,
    roles: ["Admin", "User", "Approver"],
  },
  {
    label: "Islerim",
    href: "#tasks",
    icon: ClipboardList,
    roles: ["Admin", "Approver"],
  },
  {
    label: "Ayarlar",
    href: "#settings",
    icon: Settings,
    roles: ["Admin", "User", "Approver"],
  },
];
