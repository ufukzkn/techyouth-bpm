"use client";

import { LogOut, Menu } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { NotificationMenu } from "@/features/app-shell/components/NotificationMenu";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, NotificationItem, ThemeMode, User } from "@/lib/types";

type WorkspaceTopbarProps = {
  expiresAt: string | null;
  isMobileNavOpen: boolean;
  language: Language;
  theme: ThemeMode;
  token: string | null;
  user: User;
  onLogout: () => void;
  onToggleLanguage: () => void;
  onToggleMobileNav: () => void;
  onToggleTheme: () => void;
};

export function WorkspaceTopbar({
  expiresAt,
  isMobileNavOpen,
  language,
  theme,
  token,
  user,
  onLogout,
  onToggleLanguage,
  onToggleMobileNav,
  onToggleTheme,
}: WorkspaceTopbarProps) {
  const [isSessionDetailsOpen, setIsSessionDetailsOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  const loadNotifications = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      setNotifications([]);
      return;
    }

    try {
      setNotifications(await api.listNotifications(token));
    } catch {
      setNotifications([]);
    }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadNotifications();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [loadNotifications, user.id]);

  async function markNotificationRead(notificationId: string) {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    await api.markNotificationRead(token, notificationId);
    await loadNotifications();
  }

  async function markAllNotificationsRead() {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    await api.markAllNotificationsRead(token);
    await loadNotifications();
  }

  const effectiveRole = user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadi");

  return (
    <header className="topbar">
      <button
        className="mobile-nav-toggle icon-button"
        type="button"
        aria-controls="workspace-navigation"
        aria-expanded={isMobileNavOpen}
        aria-label={t("common.menuOpen")}
        onClick={onToggleMobileNav}
      >
        <Menu size={18} />
      </button>
      <div className="topbar-user">
        <div className="session-menu compact-session-menu">
          <SessionStatusButton
            expanded={isSessionDetailsOpen}
            label={t("session.details")}
            onToggle={() => {
              setIsSessionDetailsOpen((isOpen) => !isOpen);
              setIsNotificationsOpen(false);
            }}
          />
          {isSessionDetailsOpen ? (
            <div className="session-popover" role="dialog" aria-label={t("session.details")}>
              <div><span>{t("session.user")}</span><strong>{user.displayName}</strong></div>
              <div><span>{t("session.username")}</span><strong>{user.username}</strong></div>
              <div><span>{t("session.role")}</span><strong>{effectiveRole}</strong></div>
              {user.communityName ? <div><span>{t("session.community")}</span><strong>{user.communityName}</strong></div> : null}
              <div><span>{t("session.activeUntil")}</span><strong>{formatSessionExpiry(expiresAt, language)}</strong></div>
            </div>
          ) : null}
        </div>
        <div className="topbar-identity">
          <span className="eyebrow">{t("session.activeUser")}</span>
          <strong>{user.displayName}</strong>
        </div>
        <span className="role-pill">{effectiveRole}</span>
      </div>
      <div className="topbar-actions">
        <NotificationMenu
          emptyLabel={t("notifications.empty")}
          isOpen={isNotificationsOpen}
          items={notifications}
          label={t("notifications.title")}
          markAllLabel={t("notifications.markAllRead")}
          onMarkAllRead={() => void markAllNotificationsRead()}
          onMarkRead={(notificationId) => void markNotificationRead(notificationId)}
          onToggle={() => {
            setIsNotificationsOpen((isOpen) => !isOpen);
            setIsSessionDetailsOpen(false);
          }}
        />
        <LanguageToggleButton language={language} label={t("common.language")} onToggle={onToggleLanguage} />
        <ThemeToggleButton theme={theme} label={t("common.theme")} onToggle={onToggleTheme} />
        <button className="icon-button logout-button" onClick={onLogout} aria-label={t("common.logout")} title={t("common.logout")}>
          <LogOut size={18} />
        </button>
      </div>
    </header>
  );
}
