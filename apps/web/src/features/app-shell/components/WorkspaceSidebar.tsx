"use client";

import { ChevronDown, UsersRound, X } from "lucide-react";
import Link from "next/link";
import type { NavItem } from "@/features/app-shell/navigation";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

type WorkspaceSidebarProps = {
  isManagementOpen: boolean;
  isMobileOpen: boolean;
  items: NavItem[];
  language: Language;
  pathname: string;
  onCloseMobile: () => void;
  onToggleManagement: () => void;
};

export function WorkspaceSidebar({
  isManagementOpen,
  isMobileOpen,
  items,
  language,
  pathname,
  onCloseMobile,
  onToggleManagement,
}: WorkspaceSidebarProps) {
  const managementItems = items.filter((item) => item.group === "management");
  const hasActiveManagementRoute = managementItems.some((item) => pathname === item.path);

  return (
    <>
      <aside className={isMobileOpen ? "sidebar sidebar-open" : "sidebar"} id="workspace-navigation">
        <div className="brand">
          <span className="brand-symbol">
            <PrototypeLogo size={34} />
          </span>
          <div>
            <strong>{translate(language, "app.name")}</strong>
            <span>{translate(language, "app.subtitle")}</span>
          </div>
        </div>
        <button
          className="mobile-nav-close"
          type="button"
          aria-label={translate(language, "common.menuClose")}
          onClick={onCloseMobile}
        >
          <X size={18} />
        </button>
        <nav className="side-nav" aria-label={translate(language, "app.navigation")}>
          {items.map((item, index) => {
            if (item.group === "management") {
              const isFirstManagementItem = !items
                .slice(0, index)
                .some((previousItem) => previousItem.group === "management");
              if (!isFirstManagementItem) {
                return null;
              }

              return (
                <div className="nav-disclosure" key="management">
                  <button
                    aria-expanded={isManagementOpen}
                    className={hasActiveManagementRoute ? "nav-group-trigger active" : "nav-group-trigger"}
                    onClick={onToggleManagement}
                    type="button"
                  >
                    <UsersRound size={18} />
                    <span>{translate(language, "nav.management")}</span>
                    <ChevronDown className={isManagementOpen ? "nav-group-chevron open" : "nav-group-chevron"} size={16} />
                  </button>
                  {isManagementOpen ? (
                    <div className="nav-submenu">
                      {managementItems.map((child) => {
                        const ChildIcon = child.icon;
                        return (
                          <Link
                            aria-current={pathname === child.path ? "page" : undefined}
                            className={pathname === child.path ? "active" : undefined}
                            href={child.path}
                            key={child.viewId}
                            onClick={onCloseMobile}
                          >
                            <ChildIcon size={16} />
                            {translate(language, child.labelKey)}
                          </Link>
                        );
                      })}
                    </div>
                  ) : null}
                </div>
              );
            }

            const Icon = item.icon;
            return (
              <Link
                aria-current={pathname === item.path ? "page" : undefined}
                className={pathname === item.path ? "active" : undefined}
                href={item.path}
                key={item.viewId}
                onClick={onCloseMobile}
              >
                <Icon size={18} />
                {translate(language, item.labelKey)}
              </Link>
            );
          })}
        </nav>
      </aside>
      {isMobileOpen ? (
        <button
          className="mobile-nav-backdrop"
          type="button"
          aria-label={translate(language, "common.menuClose")}
          onClick={onCloseMobile}
        />
      ) : null}
    </>
  );
}
