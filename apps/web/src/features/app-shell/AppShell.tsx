"use client";

import { LogOut, Menu, X } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { getNavItemByPath, getNavItemByView, navItems, type ViewId } from "@/features/app-shell/navigation";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { LoginView } from "@/features/auth/LoginView";
import { FormDesignerDraft } from "@/features/form-designer/FormDesignerDraft";
import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { ProcessBoardDraft } from "@/features/processes/ProcessBoardDraft";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { Language, ProcessSummary, ProcessTask, User } from "@/lib/types";

let dashboardMetricsCache: { processes: ProcessSummary[]; tasks: ProcessTask[] } | null = null;
const maxBrowserTimeoutDelay = 2_147_483_647;

export function AppShell() {
  const {
    user,
    token,
    expiresAt,
    theme,
    language,
    hasHydrated,
    expireSession,
    logout,
    syncSystemTheme,
    toggleLanguage,
    toggleTheme,
  } = useSessionStore();
  const [activeView, setActiveView] = useState<ViewId>("dashboard");
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
    document.title = language === "tr" ? "TechYouth BPM Wizard" : "TechYouth BPM Wizard";
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

    if (expiresAtTime && expiresAtTime <= Date.now()) {
      expireSession(t("session.expired"));
      return;
    }

    function scheduleExpiryCheck() {
      if (!expiresAtTime) {
        return;
      }

      const remainingMs = expiresAtTime - Date.now();
      if (remainingMs <= 0) {
        expireSession(t("session.expired"));
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
          expireSession(t("session.unverified"));
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
  }, [expiresAt, expireSession, hasHydrated, t, token, user]);

  useEffect(() => {
    function syncViewFromUrl() {
      const viewFromPath = getNavItemByPath(window.location.pathname)?.viewId;
      const requestedView = new URLSearchParams(window.location.search).get("view");
      setActiveView(viewFromPath ?? (isViewId(requestedView) ? requestedView : "dashboard"));
    }

    syncViewFromUrl();
    window.addEventListener("popstate", syncViewFromUrl);

    return () => window.removeEventListener("popstate", syncViewFromUrl);
  }, []);

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

  const changeView = useCallback((viewId: ViewId) => {
    setActiveView(viewId);
    setIsMobileNavOpen(false);
    window.history.pushState(null, "", getNavItemByView(viewId)?.path ?? "/dashboard");
  }, []);

  const visibleNavItems = useMemo(
    () => navItems.filter((item) => (user ? item.roles.includes(user.role) : false)),
    [user],
  );

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

  const currentView = visibleNavItems.some((item) => item.viewId === activeView)
    ? activeView
    : visibleNavItems[0]?.viewId ?? "dashboard";

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
                aria-current={currentView === item.viewId ? "page" : undefined}
                className={currentView === item.viewId ? "active" : undefined}
                key={item.viewId}
                onClick={() => changeView(item.viewId)}
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
              onClick={logout}
              aria-label={t("common.logout")}
              title={t("common.logout")}
            >
              <LogOut size={18} />
            </button>
          </div>
        </header>

        <main className="content">
          {currentView === "dashboard" ? (
            <DashboardView
              token={token}
              user={user}
              language={language}
              visibleViewIds={visibleNavItems.map((item) => item.viewId)}
              onNavigate={changeView}
            />
          ) : null}
          {currentView === "forms" && user.role === "Admin" ? <FormDesignerDraft /> : null}
          {currentView === "runner" ? <FormRunnerDraft /> : null}
          {currentView === "processes" ? <ProcessBoardDraft mode="processes" role={user.role} /> : null}
          {currentView === "tasks" ? <ProcessBoardDraft mode="tasks" role={user.role} /> : null}
          {currentView === "settings" ? <SettingsView expiresAt={expiresAt} language={language} /> : null}
        </main>
      </div>
    </div>
  );
}

function isViewId(value: string | null): value is ViewId {
  return navItems.some((item) => item.viewId === value);
}

function DashboardView({
  token,
  user,
  language,
  visibleViewIds,
  onNavigate,
}: {
  token: string | null;
  user: User;
  language: Language;
  visibleViewIds: ViewId[];
  onNavigate: (viewId: ViewId) => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [processes, setProcesses] = useState<ProcessSummary[]>(() => dashboardMetricsCache?.processes ?? []);
  const [tasks, setTasks] = useState<ProcessTask[]>(() => dashboardMetricsCache?.tasks ?? []);
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "error">(
    dashboardMetricsCache ? "refreshing" : "loading",
  );

  useEffect(() => {
    let ignore = false;

    async function loadMetrics() {
      if (!token || token.startsWith("demo-")) {
        setStatus("idle");
        return;
      }

      try {
        setStatus(dashboardMetricsCache ? "refreshing" : "loading");
        const [processResult, taskResult] = await Promise.all([api.listProcesses(token), api.listMyTasks(token)]);
        if (!ignore) {
          dashboardMetricsCache = { processes: processResult, tasks: taskResult };
          setProcesses(processResult);
          setTasks(taskResult);
          setStatus("idle");
        }
      } catch {
        if (!ignore) {
          setStatus("error");
        }
      }
    }

    void loadMetrics();

    return () => {
      ignore = true;
    };
  }, [token]);

  const openTaskCount = tasks.filter((task) => task.status === "Open").length;
  const inProgressCount = processes.filter((process) => process.status === "InProgress").length;
  const completedCount = processes.filter((process) => process.status === "Completed").length;
  const canOpen = useCallback((viewId: ViewId) => visibleViewIds.includes(viewId), [visibleViewIds]);

  const metricCards: Array<{ label: string; value: number; viewId?: ViewId }> = [
    { label: t("dashboard.pendingTasks"), value: openTaskCount, viewId: canOpen("tasks") ? "tasks" : undefined },
    { label: t("dashboard.inProgress"), value: inProgressCount, viewId: canOpen("processes") ? "processes" : undefined },
    { label: t("dashboard.completed"), value: completedCount, viewId: canOpen("processes") ? "processes" : undefined },
  ];

  const flowSteps: Array<{ label: string; caption: string; viewId: ViewId }> = [
    { label: t("dashboard.flow.session"), caption: t("dashboard.flow.sessionCaption"), viewId: "settings" },
    {
      label: t("dashboard.flow.formDefinition"),
      caption: t("dashboard.flow.formDefinitionCaption"),
      viewId: "forms",
    },
    {
      label: t("dashboard.flow.processInstance"),
      caption: t("dashboard.flow.processInstanceCaption"),
      viewId: "runner",
    },
    { label: t("dashboard.flow.taskAction"), caption: t("dashboard.flow.taskActionCaption"), viewId: "tasks" },
    { label: t("dashboard.flow.auditLog"), caption: t("dashboard.flow.auditLogCaption"), viewId: "processes" },
  ];

  return (
    <div className="view-panel">
      <section className="workspace-header">
        <div>
          <span className="eyebrow">{t("dashboard.eyebrow")}</span>
          <h1>{t("dashboard.title")}</h1>
        </div>
        <p>
          {status === "error"
            ? t("dashboard.error")
            : status === "loading"
              ? t("dashboard.loading")
              : t("dashboard.summary", { role: roleLabel(language, user.role) })}
        </p>
      </section>

      <section className="metric-grid" aria-label="Process summary">
        {metricCards.map((card) =>
          card.viewId ? (
            <button
              className="metric-card metric-action"
              key={card.label}
              onClick={() => onNavigate(card.viewId!)}
              type="button"
            >
              <span>{card.label}</span>
              <strong>{card.value}</strong>
            </button>
          ) : (
            <article className="metric-card" key={card.label}>
              <span>{card.label}</span>
              <strong>{card.value}</strong>
            </article>
          ),
        )}
      </section>

      <section className="flow-preview">
        {flowSteps
          .filter((step) => canOpen(step.viewId))
          .map((step) => (
            <button className="flow-step" key={step.label} onClick={() => onNavigate(step.viewId)} type="button">
              <strong>{step.label}</strong>
              <span>{step.caption}</span>
            </button>
          ))}
      </section>
    </div>
  );
}

function SettingsView({ expiresAt, language }: { expiresAt: string | null; language: Language }) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("settings.eyebrow")}</span>
          <h2>{t("settings.title")}</h2>
        </div>
        <p>{t("settings.description")}</p>
      </div>
      <div className="settings-grid">
        <article className="settings-row">
          <span>{t("settings.theme")}</span>
          <strong>{t("settings.themeValue")}</strong>
        </article>
        <article className="settings-row">
          <span>{t("settings.language")}</span>
          <strong>{t("settings.languageValue")}</strong>
        </article>
        <article className="settings-row">
          <span>{t("settings.session")}</span>
          <strong>{formatSessionExpiry(expiresAt, language)}</strong>
        </article>
        <article className="settings-row">
          <span>{t("settings.auth")}</span>
          <strong>{t("settings.authValue")}</strong>
        </article>
      </div>
    </section>
  );
}

function formatSessionExpiry(expiresAt: string | null, language: Language) {
  if (!expiresAt) {
    return translate(language, "session.noExpiry");
  }

  const expiryDate = new Date(expiresAt);
  if (Number.isNaN(expiryDate.getTime())) {
    return translate(language, "session.unknownExpiry");
  }

  return expiryDate.toLocaleString(language === "tr" ? "tr-TR" : "en-US", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
