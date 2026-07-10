"use client";

import { Bell, ChevronDown, LogOut, Menu, UsersRound, X } from "lucide-react";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { navItems, type ViewId } from "@/features/app-shell/navigation";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { ForcedPasswordChangeView } from "@/features/app-shell/views/ForcedPasswordChangeView";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError, setUnauthorizedHandler } from "@/lib/api";
import type { Language, NotificationItem, User } from "@/lib/types";

const maxBrowserTimeoutDelay = 2_147_483_647;

export type WorkspaceShellContext = {
  user: User;
  token: string | null;
  expiresAt: string | null;
  language: Language;
  visibleViewIds: ViewId[];
  navigate: (viewId: ViewId) => void;
  logout: () => void;
  setUser: (user: User) => void;
};

export function WorkspaceShell({
  viewId,
  children,
}: {
  viewId: ViewId;
  children: (context: WorkspaceShellContext) => ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const {
    user,
    token,
    expiresAt,
    theme,
    language,
    hasHydrated,
    expireSession,
    logout,
    setSession,
    setUser,
    syncSystemTheme,
    toggleLanguage,
    toggleTheme,
  } = useSessionStore();
  const [isSessionDetailsOpen, setIsSessionDetailsOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const [isManagementOpen, setIsManagementOpen] = useState(() => pathname.startsWith("/management"));
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  useEffect(() => {
    let hasHandledUnauthorized = false;
    setUnauthorizedHandler(() => {
      if (hasHandledUnauthorized) {
        return;
      }

      hasHandledUnauthorized = true;
      expireSession(t("session.unverified"));
      router.replace("/login");
    });

    return () => setUnauthorizedHandler(null);
  }, [expireSession, router, t]);

  useEffect(() => {
    document.documentElement.lang = language;
    document.title = "TechYouth BPM Wizard";
  }, [language]);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    const syncTheme = () => syncSystemTheme(mediaQuery.matches ? "dark" : "light");

    syncTheme();
    mediaQuery.addEventListener("change", syncTheme);
    return () => mediaQuery.removeEventListener("change", syncTheme);
  }, [syncSystemTheme]);

  const endSession = useCallback(() => {
    logout();
    router.replace("/login");
  }, [logout, router]);

  useEffect(() => {
    if (!hasHydrated || !token || !user) {
      return;
    }

    let ignore = false;
    let expiryTimer: number | undefined;
    const sessionToken = token;
    const expiresAtTime = expiresAt ? Date.parse(expiresAt) : null;

    async function refreshOrExpire(message: string) {
      if (sessionToken.startsWith("demo-")) {
        expireSession(message);
        return;
      }

      try {
        const refreshedSession = await api.refreshSession();
        if (!ignore) {
          setSession(refreshedSession);
        }
      } catch {
        if (!ignore) {
          expireSession(message);
        }
      }
    }

    if (expiresAtTime && expiresAtTime <= Date.now()) {
      void refreshOrExpire(t("session.expired"));
      return;
    }

    function scheduleExpiryCheck() {
      if (!expiresAtTime) {
        return;
      }

      const remainingMs = expiresAtTime - Date.now();
      if (remainingMs <= 0) {
        void refreshOrExpire(t("session.expired"));
        return;
      }

      expiryTimer = window.setTimeout(scheduleExpiryCheck, Math.min(remainingMs, maxBrowserTimeoutDelay));
    }

    if (expiresAtTime) {
      scheduleExpiryCheck();
    }

    async function verifySession() {
      if (sessionToken.startsWith("demo-")) {
        return;
      }

      try {
        await api.me(sessionToken);
      } catch (error) {
        if (ignore) {
          return;
        }

        if (error instanceof ApiError && error.statusCode === 401) {
          await refreshOrExpire(t("session.unverified"));
        }
      }
    }

    void verifySession();

    return () => {
      ignore = true;
      if (expiryTimer) {
        window.clearTimeout(expiryTimer);
      }
    };
  }, [expiresAt, expireSession, hasHydrated, setSession, t, token, user]);

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
    if (!hasHydrated || !user) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadNotifications();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [hasHydrated, loadNotifications, user]);

  useEffect(() => {
    if (!isMobileNavOpen) {
      return;
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsMobileNavOpen(false);
      }
    }

    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [isMobileNavOpen]);

  const navigate = useCallback(
    (nextViewId: ViewId) => {
      const nextItem = navItems.find((item) => item.viewId === nextViewId);
      setIsMobileNavOpen(false);
      router.push(nextItem?.path ?? "/dashboard");
    },
    [router],
  );

  const visibleNavItems = useMemo(
    () =>
      navItems.filter((item) => {
        if (!user || !canUseNavItem(user.permissions ?? [], item.permissions)) {
          return false;
        }

        return user.mustChangePassword ? item.viewId === "settings" : true;
      }),
    [user],
  );
  const canAccessCurrentRoute = visibleNavItems.some((item) => item.viewId === viewId);
  const unreadNotificationCount = notifications.filter((notification) => !notification.readAt).length;

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

  useEffect(() => {
    if (!hasHydrated || !user || user.mustChangePassword || canAccessCurrentRoute) {
      return;
    }

    router.replace(visibleNavItems[0]?.path ?? "/dashboard");
  }, [canAccessCurrentRoute, hasHydrated, router, user, visibleNavItems]);

  useEffect(() => {
    if (hasHydrated && !user) {
      router.replace("/login");
    }
  }, [hasHydrated, router, user]);

  if (!hasHydrated) {
    return <WorkspaceLoadingShell />;
  }

  if (!user) {
    return <LoginRedirectLoading />;
  }

  if (user.mustChangePassword) {
    return (
      <ForcedPasswordChangeView
        language={language}
        token={token}
        user={user}
        onLogout={endSession}
        onUserUpdated={setUser}
        onToggleLanguage={toggleLanguage}
        onToggleTheme={toggleTheme}
        theme={theme}
      />
    );
  }

  if (!canAccessCurrentRoute) {
    return <LoginRedirectLoading />;
  }

  return (
    <div className="app-shell">
      <aside className={isMobileNavOpen ? "sidebar sidebar-open" : "sidebar"} id="workspace-navigation">
        <div className="brand">
          <span className="brand-symbol">
            <PrototypeLogo size={34} />
          </span>
          <div>
            <strong>{t("app.name")}</strong>
            <span>{t("app.subtitle")}</span>
          </div>
        </div>
        <button
          className="mobile-nav-close"
          type="button"
          aria-label={t("common.menuClose")}
          onClick={() => setIsMobileNavOpen(false)}
        >
          <X size={18} />
        </button>
        <nav className="side-nav" aria-label="Main navigation">
          {visibleNavItems.map((item, index) => {
            if (item.group === "management") {
              const isFirstManagementItem = !visibleNavItems.slice(0, index).some((previousItem) => previousItem.group === "management");
              if (!isFirstManagementItem) {
                return null;
              }

              const managementItems = visibleNavItems.filter((candidate) => candidate.group === "management");
              const hasActiveManagementRoute = managementItems.some((candidate) => pathname === candidate.path);
              return (
                <div className="nav-disclosure" key="management">
                  <button
                    aria-expanded={isManagementOpen}
                    className={hasActiveManagementRoute ? "nav-group-trigger active" : "nav-group-trigger"}
                    onClick={() => setIsManagementOpen((isOpen) => !isOpen)}
                    type="button"
                  >
                    <UsersRound size={18} />
                    <span>Yonetim</span>
                    <ChevronDown className={isManagementOpen ? "nav-group-chevron open" : "nav-group-chevron"} size={16} />
                  </button>
                  {isManagementOpen ? (
                    <div className="nav-submenu">
                      {managementItems.map((child) => {
                        const ChildIcon = child.icon;
                        return (
                          <button
                            aria-current={pathname === child.path ? "page" : undefined}
                            className={pathname === child.path ? "active" : undefined}
                            key={child.viewId}
                            onClick={() => navigate(child.viewId)}
                            type="button"
                          >
                            <ChildIcon size={16} />
                            {t(child.labelKey)}
                          </button>
                        );
                      })}
                    </div>
                  ) : null}
                </div>
              );
            }
            const Icon = item.icon;
            return (
              <button
                aria-current={pathname === item.path ? "page" : undefined}
                className={pathname === item.path ? "active" : undefined}
                key={item.viewId}
                onClick={() => navigate(item.viewId)}
                type="button"
              >
                <Icon size={18} />
                {t(item.labelKey)}
              </button>
            );
          })}
        </nav>
      </aside>
      {isMobileNavOpen ? (
        <button
          className="mobile-nav-backdrop"
          type="button"
          aria-label="Menuyu kapat"
          onClick={() => setIsMobileNavOpen(false)}
        />
      ) : null}

      <div className="main-area">
        <header className="topbar">
          <button
            className="mobile-nav-toggle icon-button"
            type="button"
            aria-controls="workspace-navigation"
            aria-expanded={isMobileNavOpen}
            aria-label={t("common.menuOpen")}
            onClick={() => setIsMobileNavOpen((isOpen) => !isOpen)}
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
                  <div>
                    <span>{t("session.user")}</span>
                    <strong>{user.displayName}</strong>
                  </div>
                  <div>
                    <span>{t("session.username")}</span>
                    <strong>{user.username}</strong>
                  </div>
                  <div>
                    <span>{t("session.role")}</span>
                    <strong>{user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadi")}</strong>
                  </div>
                  {user.communityName ? (
                    <div>
                      <span>{t("session.community")}</span>
                      <strong>{user.communityName}</strong>
                    </div>
                  ) : null}
                  <div>
                    <span>{t("session.activeUntil")}</span>
                    <strong>{formatSessionExpiry(expiresAt, language)}</strong>
                  </div>
                </div>
              ) : null}
            </div>
            <div className="topbar-identity">
              <span className="eyebrow">{t("session.activeUser")}</span>
              <strong>{user.displayName}</strong>
            </div>
            <span className="role-pill">{user.communityRoleName || (user.role === "SuperAdmin" ? "SuperAdmin" : "Atanmadi")}</span>
          </div>
          <div className="topbar-actions">
            <div className="notification-menu">
              <button
                className="icon-button notification-button"
                onClick={() => {
                  setIsNotificationsOpen((isOpen) => !isOpen);
                  setIsSessionDetailsOpen(false);
                }}
                title="Bildirimler"
                type="button"
              >
                <Bell size={18} />
                {unreadNotificationCount > 0 ? <span className="notification-badge">{unreadNotificationCount}</span> : null}
              </button>
              {isNotificationsOpen ? (
                <div className="notification-popover" role="dialog" aria-label="Bildirimler">
                  <div className="notification-popover-header">
                    <strong>Bildirimler</strong>
                    <button className="text-button" type="button" onClick={markAllNotificationsRead}>
                      Tumunu okundu yap
                    </button>
                  </div>
                  <div className="notification-list">
                    {notifications.slice(0, 8).map((notification) => (
                      <button
                        className={notification.readAt ? "notification-item" : "notification-item is-unread"}
                        key={notification.id}
                        onClick={() => void markNotificationRead(notification.id)}
                        type="button"
                      >
                        <strong>{notification.title}</strong>
                        <span>{notification.message}</span>
                      </button>
                    ))}
                    {!notifications.length ? <p className="status-line">Bildirim yok.</p> : null}
                  </div>
                </div>
              ) : null}
            </div>
            <LanguageToggleButton language={language} label={t("common.language")} onToggle={toggleLanguage} />
            <ThemeToggleButton theme={theme} label={t("common.theme")} onToggle={toggleTheme} />
            <button
              className="icon-button logout-button"
              onClick={() => {
                if (token && !token.startsWith("demo-")) {
                  void api.logout(token).finally(endSession);
                  return;
                }

                endSession();
              }}
              aria-label={t("common.logout")}
              title={t("common.logout")}
            >
              <LogOut size={18} />
            </button>
          </div>
        </header>

        <main className="content">
          {children({
            user,
            token,
            expiresAt,
            language,
            visibleViewIds: visibleNavItems.map((item) => item.viewId),
            navigate,
            logout: endSession,
            setUser,
          })}
        </main>
      </div>
    </div>
  );
}

function WorkspaceLoadingShell() {
  return (
    <div className="app-shell workspace-loading-shell" aria-live="polite" aria-label="Calisma alani yukleniyor">
      <aside className="sidebar workspace-loading-sidebar">
        <div className="brand"><span className="brand-symbol"><PrototypeLogo size={34} /></span><div><strong>TechYouth BPM</strong><span>Workspace</span></div></div>
        <div className="workspace-loading-nav"><span /><span /><span /><span /><span /></div>
      </aside>
      <div className="main-area"><header className="topbar workspace-loading-topbar"><span /><span /></header><main className="content"><div className="workspace-loading-content"><span /><span /><span /></div></main></div>
    </div>
  );
}

function LoginRedirectLoading() {
  return (
    <main className="login-page" aria-live="polite">
      <section className="login-panel session-loading">
        <PrototypeLogo size={44} />
        <span className="eyebrow">Oturum</span>
        <h1>Giris ekranina yonlendiriliyor</h1>
      </section>
    </main>
  );
}

function canUseNavItem(userPermissions: string[], requiredPermissions?: string[]) {
  if (!requiredPermissions?.length) {
    return true;
  }

  return requiredPermissions.some((permission) => userPermissions.includes(permission));
}
