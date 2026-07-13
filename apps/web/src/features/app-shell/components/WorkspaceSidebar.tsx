"use client";

import { ChevronDown, X } from "lucide-react";
import Link from "next/link";
import { navGroups, type NavGroupId, type NavItem } from "@/features/app-shell/navigation";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

type WorkspaceSidebarProps = {
  isMobileOpen: boolean;
  items: NavItem[];
  language: Language;
  openGroups: NavGroupId[];
  pathname: string;
  onCloseMobile: () => void;
  onToggleGroup: (groupId: NavGroupId) => void;
};

export function WorkspaceSidebar({
  isMobileOpen,
  items,
  language,
  openGroups,
  pathname,
  onCloseMobile,
  onToggleGroup,
}: WorkspaceSidebarProps) {
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
            if (item.group) {
              const groupId = item.group;
              const groupItems = items.filter((candidate) => candidate.group === groupId);
              const isFirstGroupItem = !items.slice(0, index).some((previousItem) => previousItem.group === groupId);
              if (!isFirstGroupItem) {
                return null;
              }

              const group = navGroups[groupId];
              const GroupIcon = group.icon;
              const hasActiveRoute = groupItems.some((candidate) => pathname === candidate.path);
              const isGroupOpen = hasActiveRoute || openGroups.includes(groupId);

              return (
                <div className="nav-disclosure" key={groupId}>
                  <button
                    aria-expanded={isGroupOpen}
                    className={hasActiveRoute ? "nav-group-trigger active" : "nav-group-trigger"}
                    onClick={() => onToggleGroup(groupId)}
                    type="button"
                  >
                    <GroupIcon size={18} />
                    <span>{translate(language, group.labelKey)}</span>
                    <ChevronDown className={isGroupOpen ? "nav-group-chevron open" : "nav-group-chevron"} size={16} />
                  </button>
                  {isGroupOpen ? (
                    <div className="nav-submenu">
                      {groupItems.map((child) => {
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
