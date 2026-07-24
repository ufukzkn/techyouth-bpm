"use client";

import { ListTodo, LogOut, Menu } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { MobileTopbarActionsMenu } from "@/features/app-shell/components/MobileTopbarActionsMenu";
import { NotificationMenu } from "@/features/app-shell/components/NotificationMenu";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { getNotificationTarget } from "@/features/notifications/notificationNavigation";
import { useNotificationStore } from "@/features/notifications/notificationStore";
import { NotificationLiveToasts } from "@/features/notifications/NotificationLiveToasts";
import type { Language, NotificationItem, ThemeMode, User } from "@/lib/types";

type WorkspaceTopbarProps = {
  expiresAt: string | null;
  isMobileNavOpen: boolean;
  canAccessTasks: boolean;
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
  canAccessTasks,
  language,
  theme,
  token,
  user,
  onLogout,
  onToggleLanguage,
  onToggleMobileNav,
  onToggleTheme,
}: WorkspaceTopbarProps) {
  const router = useRouter();
  const [isSessionDetailsOpen, setIsSessionDetailsOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [isMobileActionsOpen, setIsMobileActionsOpen] = useState(false);
  const {
    previewItems,
    unreadCount,
    isLoading: isNotificationsLoading,
    pendingReadIds,
    loadPreview,
    liveToasts,
    markAllRead,
    dismissLiveToast,
    reset: resetNotifications,
    setReadState,
  } = useNotificationStore();
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const closeMobileActions = useCallback(() => setIsMobileActionsOpen(false), []);

  const loadNotifications = useCallback(async (source: "initial" | "poll" | "visibility" | "popover") => {
    if (!token || token.startsWith("demo-")) {
      resetNotifications(user.id);
      return;
    }

    try {
      await loadPreview(token, user.id, source);
    } catch {
      // The workspace keeps running when notification refresh fails.
    }
  }, [loadPreview, resetNotifications, token, user.id]);

  useEffect(() => {
    void loadNotifications("initial");
    const interval = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void loadNotifications("poll");
      }
    }, 30_000);
    function handleVisibilityChange() {
      if (document.visibilityState === "visible") {
        void loadNotifications("visibility");
      }
    }
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [loadNotifications]);

  useEffect(() => {
    if (isNotificationsOpen) {
      void loadNotifications("popover");
    }
  }, [isNotificationsOpen, loadNotifications]);

  async function openNotification(notification: NotificationItem) {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    if (!notification.readAt) {
      await setReadState(token, notification.id, true);
    }
    setIsNotificationsOpen(false);
    const target = getNotificationTarget(notification);
    if (target) {
      router.push(target);
    }
  }

  async function markAllNotificationsRead() {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    await markAllRead(token);
  }

  async function setNotificationReadState(notification: NotificationItem, isRead: boolean) {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    try {
      await setReadState(token, notification.id, isRead);
    } catch {
      // Keep the popover open and let the optimistic store state roll back on a failed request.
    }
  }

  async function openLiveNotification(notification: NotificationItem) {
    dismissLiveToast(notification.id);
    if (token && !token.startsWith("demo-") && !notification.readAt) {
      try {
        await setReadState(token, notification.id, true);
      } catch {
        return;
      }
    }
    router.push(getNotificationTarget(notification) ?? "/inbox");
  }

  const effectiveRole = user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadı");

  return (
    <header className="topbar">
      <button
        className="mobile-nav-toggle icon-button"
        type="button"
        aria-controls="workspace-navigation"
        aria-expanded={isMobileNavOpen}
        aria-label={t("common.menuOpen")}
        onClick={() => {
          setIsSessionDetailsOpen(false);
          setIsNotificationsOpen(false);
          closeMobileActions();
          onToggleMobileNav();
        }}
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
              setIsMobileActionsOpen(false);
            }}
          />
          {isSessionDetailsOpen ? (
            <div className="session-popover" role="dialog" aria-label={t("session.details")}>
              <div><span>{t("session.user")}</span><strong>{user.displayName}</strong></div>
              <div><span>{t("session.username")}</span><strong>{user.username}</strong></div>
              <div><span>{t("session.role")}</span><strong>{effectiveRole}</strong></div>
              {user.communityName ? <div><span>{t("session.community")}</span><strong>{user.communityName}</strong></div> : null}
              {user.communityId ? <div><span>{t("session.teams")}</span><strong>{(user.teams ?? []).length ? (user.teams ?? []).map((team) => team.name).join(", ") : t("dashboard.unassignedTeam")}</strong></div> : null}
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
        {canAccessTasks ? (
          <button
            aria-label={t("common.openMyTasks")}
            className="icon-button tasks-shortcut-button"
            onClick={() => router.push("/tasks?view=active")}
            title={t("common.openMyTasks")}
            type="button"
          >
            <ListTodo size={18} />
          </button>
        ) : null}
        <NotificationMenu
          emptyLabel={t("notifications.empty")}
          inboxLabel={t("notifications.openInbox")}
          isOpen={isNotificationsOpen}
          isLoading={isNotificationsLoading}
          items={previewItems}
          label={t("notifications.title")}
          markAllLabel={t("notifications.markAllRead")}
          markReadLabel={t("inbox.markRead")}
          markUnreadLabel={t("inbox.markUnread")}
          pendingReadIds={pendingReadIds}
          unreadCount={unreadCount}
          onMarkAllRead={() => void markAllNotificationsRead()}
          onOpenInbox={() => {
            setIsNotificationsOpen(false);
            router.push("/inbox");
          }}
          onSelect={(notification) => void openNotification(notification)}
          onSetReadState={(notification, isRead) => void setNotificationReadState(notification, isRead)}
          onToggle={() => {
            setIsNotificationsOpen((isOpen) => !isOpen);
            setIsSessionDetailsOpen(false);
            setIsMobileActionsOpen(false);
          }}
        />
        <LanguageToggleButton language={language} label={t("common.language")} onToggle={onToggleLanguage} />
        <ThemeToggleButton theme={theme} label={t("common.theme")} onToggle={onToggleTheme} />
        <button className="icon-button logout-button" onClick={onLogout} aria-label={t("common.logout")} title={t("common.logout")}>
          <LogOut size={18} />
        </button>
        <MobileTopbarActionsMenu
          canAccessTasks={canAccessTasks}
          isOpen={isMobileActionsOpen}
          language={language}
          onClose={closeMobileActions}
          onLogout={onLogout}
          onOpenTasks={() => router.push("/tasks?view=active")}
          onToggle={() => {
            setIsMobileActionsOpen((isOpen) => !isOpen);
            setIsSessionDetailsOpen(false);
            setIsNotificationsOpen(false);
          }}
          onToggleLanguage={onToggleLanguage}
          onToggleTheme={onToggleTheme}
          theme={theme}
        />
      </div>
      <NotificationLiveToasts
        onDismiss={dismissLiveToast}
        onSelect={(notification) => void openLiveNotification(notification)}
        toasts={liveToasts}
      />
    </header>
  );
}
