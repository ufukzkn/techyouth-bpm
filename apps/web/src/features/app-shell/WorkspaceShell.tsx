"use client";

import { LogOut, Menu, X } from "lucide-react";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { navItems, type ViewId } from "@/features/app-shell/navigation";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { ForcedPasswordChangeView } from "@/features/app-shell/views/ForcedPasswordChangeView";
import { LoginView } from "@/features/auth/LoginView";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { Language, User } from "@/lib/types";

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
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

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
        if (!user || !item.roles.includes(user.role)) {
          return false;
        }

        return user.mustChangePassword ? item.viewId === "settings" : true;
      }),
    [user],
  );
  const canAccessCurrentRoute = visibleNavItems.some((item) => item.viewId === viewId);

  useEffect(() => {
    if (!hasHydrated || !user || user.mustChangePassword || canAccessCurrentRoute) {
      return;
    }

    router.replace(visibleNavItems[0]?.path ?? "/dashboard");
  }, [canAccessCurrentRoute, hasHydrated, router, user, visibleNavItems]);

  if (!hasHydrated) {
    return (
      <main className="login-page">
        <section className="login-panel session-loading" aria-live="polite">
          <PrototypeLogo size={44} />
          <span className="eyebrow">{t("login.session")}</span>
          <h1>{t("login.preparing")}</h1>
          <p>{t("login.checkingStoredSession")}</p>
        </section>
      </main>
    );
  }

  if (!user) {
    return <LoginView />;
  }

  if (user.mustChangePassword) {
    return (
      <ForcedPasswordChangeView
        language={language}
        token={token}
        user={user}
        onLogout={logout}
        onUserUpdated={setUser}
        onToggleLanguage={toggleLanguage}
        onToggleTheme={toggleTheme}
        theme={theme}
      />
    );
  }

  if (!canAccessCurrentRoute) {
    return (
      <main className="login-page">
        <section className="login-panel session-loading" aria-live="polite">
          <PrototypeLogo size={44} />
          <span className="eyebrow">{t("login.session")}</span>
          <h1>{t("login.preparing")}</h1>
          <p>{t("login.checkingStoredSession")}</p>
        </section>
      </main>
    );
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
          {visibleNavItems.map((item) => {
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
            <div className="topbar-identity">
              <span className="eyebrow">{t("session.activeUser")}</span>
              <strong>{user.displayName}</strong>
            </div>
            <span className="role-pill">{roleLabel(language, user.role)}</span>
          </div>
          <div className="topbar-actions">
            <div className="session-menu">
              <SessionStatusButton
                expanded={isSessionDetailsOpen}
                label={t("session.details")}
                onToggle={() => setIsSessionDetailsOpen((isOpen) => !isOpen)}
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
                    <strong>{roleLabel(language, user.role)}</strong>
                  </div>
                  <div>
                    <span>{t("session.activeUntil")}</span>
                    <strong>{formatSessionExpiry(expiresAt, language)}</strong>
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
                  void api.logout(token).finally(logout);
                  return;
                }

                logout();
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
            logout,
            setUser,
          })}
        </main>
      </div>
    </div>
  );
}
