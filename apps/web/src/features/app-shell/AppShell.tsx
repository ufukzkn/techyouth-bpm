"use client";

import { CheckCircle2, ChevronLeft, ChevronRight, History, LogOut, Menu, Search, ShieldCheck, UserCog, X } from "lucide-react";
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
import type {
  Language,
  ProcessSummary,
  ProcessTask,
  Role,
  SystemAuditLog,
  User,
  UserAdmin,
  UserSession,
  UserStatus,
} from "@/lib/types";

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
    setUser,
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
          {currentView === "users" && user.role === "Admin" ? (
            <UsersAndRolesView language={language} token={token} />
          ) : null}
          {currentView === "logs" && user.role === "Admin" ? <SystemLogsView language={language} token={token} /> : null}
          {currentView === "settings" ? (
            <SettingsView
              expiresAt={expiresAt}
              language={language}
              token={token}
              user={user}
              onLogout={logout}
              onUserUpdated={setUser}
            />
          ) : null}
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

function SettingsView({
  expiresAt,
  language,
  token,
  user,
  onLogout,
  onUserUpdated,
}: {
  expiresAt: string | null;
  language: Language;
  token: string | null;
  user: User;
  onLogout: () => void;
  onUserUpdated: (user: User) => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [verificationCode, setVerificationCode] = useState("");
  const [demoCode, setDemoCode] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [isLoadingSettings, setIsLoadingSettings] = useState(false);

  const isApiSession = Boolean(token && !token.startsWith("demo-"));

  const loadIdentityData = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    setIsLoadingSettings(true);
    try {
      const sessionResult = await api.listSessions(token);
      setSessions(sessionResult);
    } catch (error) {
      setStatusMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.loadFailed"));
    } finally {
      setIsLoadingSettings(false);
    }
  }, [t, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadIdentityData();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadIdentityData]);

  async function requestVerification() {
    if (!token) {
      return;
    }

    try {
      const response = await api.startEmailVerification(token);
      setDemoCode(response.demoCode);
      setStatusMessage(t("settings.verificationCodeReady"));
    } catch (error) {
      setStatusMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.verificationFailed"));
    }
  }

  async function confirmVerification() {
    if (!token || !verificationCode.trim()) {
      return;
    }

    try {
      const updatedUser = await api.confirmEmailVerification(token, verificationCode);
      onUserUpdated(updatedUser);
      setVerificationCode("");
      setDemoCode(null);
      setStatusMessage(t("settings.emailVerified"));
    } catch (error) {
      setStatusMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.verificationFailed"));
    }
  }

  async function revokeSession(sessionId: string, isCurrent: boolean) {
    if (!token) {
      return;
    }

    try {
      await api.revokeSession(token, sessionId);
      if (isCurrent) {
        onLogout();
        return;
      }

      await loadIdentityData();
      setStatusMessage(t("settings.sessionRevoked"));
    } catch (error) {
      setStatusMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.sessionRevokeFailed"));
    }
  }

  async function revokeAllSessions() {
    if (!token) {
      return;
    }

    try {
      const nonCurrentSessions = sessions.filter((session) => !session.isCurrent);
      const currentSession = sessions.find((session) => session.isCurrent);

      for (const session of nonCurrentSessions) {
        await api.revokeSession(token, session.id);
      }

      if (currentSession) {
        await api.revokeSession(token, currentSession.id);
        onLogout();
        return;
      }

      await loadIdentityData();
      setStatusMessage(t("settings.allSessionsRevoked"));
    } catch (error) {
      setStatusMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.sessionRevokeFailed"));
    }
  }

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
          <span>{t("settings.profile")}</span>
          <strong>{user.displayName}</strong>
          <small>{user.email || t("settings.noEmail")}</small>
        </article>
        <article className="settings-row">
          <span>{t("settings.emailStatus")}</span>
          <strong>{user.isEmailVerified ? t("settings.verified") : t("settings.notVerified")}</strong>
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

      {statusMessage ? <div className="form-success">{statusMessage}</div> : null}

      <section className="identity-section">
        <div className="section-toolbar">
          <div>
            <span className="eyebrow">{t("settings.emailVerification")}</span>
            <h3>{t("settings.emailVerificationTitle")}</h3>
          </div>
          <CheckCircle2 size={22} />
        </div>
        <p className="helper-copy">{t("settings.emailVerificationDescription")}</p>
        {user.isEmailVerified ? (
          <div className="form-success">{t("settings.emailAlreadyVerified")}</div>
        ) : (
          <div className="inline-form">
            <button className="secondary-button" type="button" disabled={!isApiSession} onClick={requestVerification}>
              {t("settings.sendVerificationCode")}
            </button>
            <input
              value={verificationCode}
              onChange={(event) => setVerificationCode(event.target.value)}
              placeholder={t("settings.verificationCode")}
            />
            <button className="primary-button" type="button" disabled={!isApiSession} onClick={confirmVerification}>
              {t("settings.verifyEmail")}
            </button>
          </div>
        )}
        {demoCode ? <p className="status-line">{t("settings.demoVerificationCode", { code: demoCode })}</p> : null}
      </section>

      <section className="identity-section">
        <div className="section-toolbar">
          <div>
            <span className="eyebrow">{t("settings.sessions")}</span>
            <h3>{t("settings.sessionsTitle")}</h3>
          </div>
          <ShieldCheck size={22} />
        </div>
        <p className="helper-copy">{t("settings.sessionsDescription")}</p>
        <div className="section-actions">
          <button className="secondary-button danger-button" type="button" disabled={!isApiSession} onClick={revokeAllSessions}>
            {t("settings.revokeAllSessions")}
          </button>
        </div>
        {isLoadingSettings ? <p className="status-line">{t("common.loading")}</p> : null}
        <div className="session-list">
          {sessions.map((session) => (
            <article className="settings-row session-row" key={session.id}>
              <div>
                <span>{session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}</span>
                <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                <small>
                  {session.lastSeenAt
                    ? t("settings.lastSeen", { value: formatSessionExpiry(session.lastSeenAt, language) })
                    : t("settings.notSeenYet")}
                </small>
              </div>
              <button
                className="secondary-button danger-button"
                type="button"
                disabled={!isApiSession}
                onClick={() => revokeSession(session.id, session.isCurrent)}
              >
                {t("settings.revokeSession")}
              </button>
            </article>
          ))}
          {!sessions.length && !isLoadingSettings ? <p className="status-line">{t("settings.noSessions")}</p> : null}
        </div>
      </section>
    </section>
  );
}

function UsersAndRolesView({ language, token }: { language: Language; token: string | null }) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<UserStatus | "All">("PendingApproval");
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const pageSize = 6;

  const loadUsersAndLogs = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    setIsLoading(true);
    try {
      const [userResult, auditResult] = await Promise.all([api.listUsers(token), api.listSystemAuditLogs(token)]);
      setUsers(userResult);
      setLogs(auditResult);
      setSelectedUserId((current) => current ?? userResult.find((item) => item.status === "PendingApproval")?.id ?? null);
    } catch (error) {
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }, [t, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsersAndLogs();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadUsersAndLogs]);

  const filteredUsers = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return users.filter((managedUser) => {
      const matchesStatus = statusFilter === "All" || managedUser.status === statusFilter;
      const matchesSearch =
        !query ||
        managedUser.username.toLowerCase().includes(query) ||
        managedUser.displayName.toLowerCase().includes(query) ||
        managedUser.email.toLowerCase().includes(query);

      return matchesStatus && matchesSearch;
    });
  }, [searchQuery, statusFilter, users]);

  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleUsers = filteredUsers.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const selectedUser = filteredUsers.find((managedUser) => managedUser.id === selectedUserId) ?? visibleUsers[0] ?? null;
  const effectiveSelectedUserId = selectedUser?.id ?? null;
  const selectedUsername = selectedUser?.username.toLowerCase();
  const selectedUserLogs = effectiveSelectedUserId
    ? logs
        .filter(
          (log) =>
            log.actorUserId === effectiveSelectedUserId ||
            (log.entityType === "User" && log.entityId === effectiveSelectedUserId) ||
            (selectedUsername ? log.actorUsername.toLowerCase() === selectedUsername : false),
        )
        .sort((left, right) => new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime())
    : [];

  async function updateUserAccess(userId: string, role: Role, status: UserStatus) {
    if (!token) {
      return;
    }

    try {
      await api.updateUserAccess(token, userId, role, status);
      await loadUsersAndLogs();
      setMessage(t("settings.userAccessUpdated"));
    } catch (error) {
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("settings.userAccessFailed"));
    }
  }

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("users.eyebrow")}</span>
          <h2>{t("users.title")}</h2>
        </div>
        <p>{t("users.description")}</p>
      </div>

      <div className="identity-section">
        <div className="filter-toolbar">
          <label className="search-field">
            <Search size={16} />
            <input
              value={searchQuery}
              onChange={(event) => {
                setSearchQuery(event.target.value);
                setPage(1);
              }}
              placeholder={t("users.searchPlaceholder")}
            />
          </label>
          <select
            value={statusFilter}
            onChange={(event) => {
              setStatusFilter(event.target.value as UserStatus | "All");
              setPage(1);
            }}
          >
            <option value="PendingApproval">{t("settings.statusPending")}</option>
            <option value="Active">{t("settings.statusActive")}</option>
            <option value="Rejected">{t("settings.statusRejected")}</option>
            <option value="All">{t("users.statusAll")}</option>
          </select>
        </div>
        {message ? <div className="form-success">{message}</div> : null}
        {isLoading ? <p className="status-line">{t("common.loading")}</p> : null}
      </div>

      <div className="management-layout">
        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.listEyebrow")}</span>
              <h3>{t("users.listTitle")}</h3>
            </div>
            <UserCog size={22} />
          </div>
          <div className="user-management-list">
            {visibleUsers.map((managedUser) => (
              <article className="settings-row user-management-row" key={managedUser.id}>
                <div className="stacked-summary">
                  <span className={`status-pill status-${managedUser.status.toLowerCase()}`}>
                    {userStatusLabel(language, managedUser.status)}
                  </span>
                  <strong>{managedUser.displayName}</strong>
                  <small>
                    {managedUser.username} / {managedUser.email}
                  </small>
                </div>
                <button className="secondary-button" type="button" onClick={() => setSelectedUserId(managedUser.id)}>
                  {t("users.viewDetail")}
                </button>
              </article>
            ))}
            {!visibleUsers.length && !isLoading ? <p className="status-line">{t("users.empty")}</p> : null}
          </div>
          <PaginationControls
            currentPage={currentPage}
            language={language}
            onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>

        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.detailEyebrow")}</span>
              <h3>{selectedUser ? selectedUser.displayName : t("users.noSelection")}</h3>
            </div>
            <History size={22} />
          </div>
          {selectedUser ? (
            <>
              <div className="settings-grid compact-grid">
                <article className="settings-row">
                  <span>{t("session.username")}</span>
                  <strong>{selectedUser.username}</strong>
                </article>
                <article className="settings-row">
                  <span>{t("session.role")}</span>
                  <strong>{roleLabel(language, selectedUser.role)}</strong>
                </article>
                <article className="settings-row">
                  <span>{t("settings.emailStatus")}</span>
                  <strong>{selectedUser.isEmailVerified ? t("settings.verified") : t("settings.notVerified")}</strong>
                </article>
              </div>
              <div className="access-editor">
                <select
                  value={selectedUser.role}
                  onChange={(event) =>
                    updateUserAccess(selectedUser.id, event.target.value as Role, selectedUser.status)
                  }
                >
                  <option value="Admin">Admin</option>
                  <option value="User">User</option>
                  <option value="Approver">Approver</option>
                </select>
                <select
                  value={selectedUser.status}
                  onChange={(event) =>
                    updateUserAccess(selectedUser.id, selectedUser.role, event.target.value as UserStatus)
                  }
                >
                  <option value="PendingApproval">{t("settings.statusPending")}</option>
                  <option value="Active">{t("settings.statusActive")}</option>
                  <option value="Rejected">{t("settings.statusRejected")}</option>
                </select>
              </div>
              <SystemAuditTimeline logs={selectedUserLogs} language={language} emptyText={t("users.noUserLogs")} />
            </>
          ) : (
            <p className="status-line">{t("users.noSelectionHelp")}</p>
          )}
        </section>
      </div>
    </section>
  );
}

function SystemLogsView({ language, token }: { language: Language; token: string | null }) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const pageSize = 8;

  useEffect(() => {
    let ignore = false;

    async function loadLogs() {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      setIsLoading(true);
      try {
        const result = await api.listSystemAuditLogs(token);
        if (!ignore) {
          setLogs(result);
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    void loadLogs();

    return () => {
      ignore = true;
    };
  }, [token]);

  const trimmedQuery = searchQuery.trim().toLowerCase();
  const filteredLogs = useMemo(() => {
    if (trimmedQuery.length < 2) {
      return [];
    }

    return logs.filter((log) =>
      [
        log.actorDisplayName,
        log.actorUsername,
        log.action,
        log.entityType,
        log.entityId ?? "",
        log.description,
      ]
        .join(" ")
        .toLowerCase()
        .includes(trimmedQuery),
    );
  }, [logs, trimmedQuery]);

  const totalPages = Math.max(1, Math.ceil(filteredLogs.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = filteredLogs.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const selectedLog = logs.find((log) => log.id === selectedLogId) ?? null;
  const relatedLogs = useMemo(() => {
    if (!selectedLog) {
      return [];
    }

    return logs
      .filter(
        (log) =>
          (selectedLog.entityId && log.entityId === selectedLog.entityId && log.entityType === selectedLog.entityType) ||
          (selectedLog.actorUserId && log.actorUserId === selectedLog.actorUserId),
      )
      .sort((left, right) => new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime());
  }, [logs, selectedLog]);

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("logs.eyebrow")}</span>
          <h2>{t("logs.title")}</h2>
        </div>
        <p>{t("logs.description")}</p>
      </div>

      <section className="identity-section">
        <label className="search-field">
          <Search size={16} />
          <input
            value={searchQuery}
            onChange={(event) => {
              setSearchQuery(event.target.value);
              setPage(1);
            }}
            placeholder={t("logs.searchPlaceholder")}
          />
        </label>
        {isLoading ? <p className="status-line">{t("common.loading")}</p> : null}
        {trimmedQuery.length < 2 ? <p className="helper-copy">{t("logs.searchFirst")}</p> : null}
      </section>

      <div className="management-layout">
        <section className="identity-section">
          <div className="system-audit-list">
            {visibleLogs.map((log) => (
              <article className="settings-row system-audit-row" key={log.id}>
                <div className="system-audit-content">
                  <span>{log.action}</span>
                  <strong>{log.description}</strong>
                  <small>
                    {log.actorDisplayName} / {log.actorUsername}
                  </small>
                </div>
                <div className="system-audit-meta">
                  <strong>{log.entityType}</strong>
                  <small>{new Date(log.createdAt).toLocaleString(language === "tr" ? "tr-TR" : "en-US")}</small>
                  <button className="secondary-button" type="button" onClick={() => setSelectedLogId(log.id)}>
                    {t("logs.viewRelated")}
                  </button>
                </div>
              </article>
            ))}
            {trimmedQuery.length >= 2 && !visibleLogs.length && !isLoading ? (
              <p className="status-line">{t("logs.empty")}</p>
            ) : null}
          </div>
          <PaginationControls
            currentPage={currentPage}
            language={language}
            onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>

        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("logs.relatedEyebrow")}</span>
              <h3>{selectedLog ? selectedLog.entityType : t("logs.noSelection")}</h3>
            </div>
            <History size={22} />
          </div>
          <SystemAuditTimeline logs={relatedLogs} language={language} emptyText={t("logs.noRelated")} />
        </section>
      </div>
    </section>
  );
}

function SystemAuditTimeline({
  logs,
  language,
  emptyText,
}: {
  logs: SystemAuditLog[];
  language: Language;
  emptyText: string;
}) {
  if (logs.length === 0) {
    return <p className="status-line">{emptyText}</p>;
  }

  return (
    <div className="system-audit-timeline">
      {logs.map((log) => (
        <article className="system-audit-event" key={log.id}>
          <span>{log.action}</span>
          <strong>{log.description}</strong>
          <small>
            {log.actorDisplayName} / {log.actorUsername} -{" "}
            {new Date(log.createdAt).toLocaleString(language === "tr" ? "tr-TR" : "en-US")}
          </small>
        </article>
      ))}
    </div>
  );
}

function PaginationControls({
  currentPage,
  language,
  onNext,
  onPrevious,
  totalPages,
}: {
  currentPage: number;
  language: Language;
  onNext: () => void;
  onPrevious: () => void;
  totalPages: number;
}) {
  return (
    <div className="pagination-controls">
      <button className="icon-button" type="button" disabled={currentPage <= 1} onClick={onPrevious}>
        <ChevronLeft size={16} />
      </button>
      <span>{translate(language, "common.page", { current: currentPage, total: totalPages })}</span>
      <button className="icon-button" type="button" disabled={currentPage >= totalPages} onClick={onNext}>
        <ChevronRight size={16} />
      </button>
    </div>
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

function userStatusLabel(language: Language, status: UserStatus) {
  return translate(language, `userStatus.${status}` as TranslationKey);
}
