"use client";

import {
  AlertTriangle,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Filter,
  History,
  KeyRound,
  LogOut,
  Menu,
  RefreshCw,
  Save,
  Search,
  ShieldCheck,
  Sparkles,
  UserCog,
  UserPlus,
  X,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { getNavItemByPath, getNavItemByView, navItems, type ViewId } from "@/features/app-shell/navigation";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { SessionStatusButton } from "@/features/app-shell/SessionStatusButton";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { LoginView } from "@/features/auth/LoginView";
import { FormDesignerDraft } from "@/features/form-designer/FormDesignerDraft";
import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { ProcessBoardDraft } from "@/features/processes/ProcessBoardDraft";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type {
  Language,
  PagedResult,
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
const emailVerificationResendCooldownMs = 5 * 60 * 1000;
type AuditCategory = "all" | "identity" | "access" | "forms" | "processes" | "tasks";
type PendingAccessChange = {
  userId: string;
  displayName: string;
  username: string;
  fromRole: Role;
  toRole: Role;
  fromStatus: UserStatus;
  toStatus: UserStatus;
};
type PendingSessionRevoke = {
  userId: string;
  sessionId: string;
  displayName: string;
  username: string;
  expiresAt: string;
  isCurrent: boolean;
};
type PendingUserDelete = {
  userId: string;
  displayName: string;
  username: string;
};
type StatusTone = "success" | "error" | "info";
type SettingsSectionId = "profile" | "password" | "sessions";
type AuditHistoryMode = "related" | "actor" | "target";
type SelectedAuditHistory = {
  logId: string;
  mode: AuditHistoryMode;
};
type AccessDraft = {
  userId: string;
  role: Role;
  status: UserStatus;
};

const auditCategories: AuditCategory[] = ["all", "identity", "access", "forms", "processes", "tasks"];

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
    () =>
      navItems.filter((item) => {
        if (!user || !item.roles.includes(user.role)) {
          return false;
        }

        return user.mustChangePassword ? item.viewId === "settings" : true;
      }),
    [user],
  );

  useEffect(() => {
    if (user?.mustChangePassword && window.location.pathname !== "/settings") {
      const timer = window.setTimeout(() => {
        setActiveView("settings");
        window.history.replaceState(null, "", "/settings");
      }, 0);

      return () => window.clearTimeout(timer);
    }
  }, [user?.mustChangePassword]);

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

  const currentView = user.mustChangePassword
    ? "settings"
    : visibleNavItems.some((item) => item.viewId === activeView)
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
            <UsersAndRolesView activeUser={user} language={language} token={token} />
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

function ForcedPasswordChangeView({
  language,
  token,
  user,
  theme,
  onLogout,
  onToggleLanguage,
  onToggleTheme,
  onUserUpdated,
}: {
  language: Language;
  token: string | null;
  user: User;
  theme: "light" | "dark";
  onLogout: () => void;
  onToggleLanguage: () => void;
  onToggleTheme: () => void;
  onUserUpdated: (user: User) => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [messageTone, setMessageTone] = useState<StatusTone>("info");
  const [isSaving, setIsSaving] = useState(false);
  const isApiSession = !!token && !token.startsWith("demo-");
  const messageClassName =
    messageTone === "error" ? "form-error" : messageTone === "success" ? "form-success" : "form-info";

  async function changePassword() {
    if (!token) {
      return;
    }

    setIsSaving(true);
    setMessage(null);
    setMessageTone("info");
    try {
      const updatedUser = await api.changePassword(token, {
        currentPassword,
        newPassword,
      });
      setCurrentPassword("");
      setNewPassword("");
      onUserUpdated(updatedUser);
      setMessage(t("settings.passwordChanged"));
      setMessageTone("success");
    } catch (error) {
      setMessage(localizeApiError(error, language, t("settings.passwordFailed")));
      setMessageTone("error");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <main className="login-page force-password-page">
      <div className="login-actions">
        <LanguageToggleButton language={language} label={t("common.language")} onToggle={onToggleLanguage} />
        <ThemeToggleButton theme={theme} label={t("common.theme")} onToggle={onToggleTheme} />
        <button className="icon-button logout-button" type="button" onClick={onLogout} title={t("common.logout")}>
          <LogOut size={18} />
        </button>
      </div>
      <section className="action-dialog force-password-dialog" aria-live="polite" role="dialog">
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("settings.mustChangePasswordTitle")}</span>
            <strong>{t("settings.forcePasswordTitle")}</strong>
          </div>
          <AlertTriangle size={24} />
        </div>
        <p className="helper-copy">
          {t("settings.forcePasswordDescription", { username: user.username })}
        </p>
        {message ? <div className={messageClassName}>{message}</div> : null}
        <div className="compact-form">
          <input
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            placeholder={t("settings.currentPassword")}
            type="password"
          />
          <input
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            placeholder={t("settings.newPassword")}
            type="password"
          />
          <button
            className="primary-button danger-button"
            type="button"
            disabled={!isApiSession || isSaving || !currentPassword || !newPassword}
            onClick={changePassword}
          >
            {isSaving ? t("common.saving") : t("settings.changePassword")}
          </button>
        </div>
      </section>
    </main>
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
  const [profileDisplayName, setProfileDisplayName] = useState(user.displayName);
  const [profileEmail, setProfileEmail] = useState(user.email);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [verificationCode, setVerificationCode] = useState("");
  const [demoCode, setDemoCode] = useState<string | null>(null);
  const [verificationExpiresAt, setVerificationExpiresAt] = useState<string | null>(null);
  const [verificationResendAvailableAt, setVerificationResendAvailableAt] = useState<string | null>(null);
  const [verificationResendSecondsLeft, setVerificationResendSecondsLeft] = useState(0);
  const [isEmailVerificationOpen, setIsEmailVerificationOpen] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusTone, setStatusTone] = useState<StatusTone>("info");
  const [isLoadingSettings, setIsLoadingSettings] = useState(false);
  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [isRequestingVerification, setIsRequestingVerification] = useState(false);
  const [pendingOwnSessionRevoke, setPendingOwnSessionRevoke] = useState<PendingSessionRevoke | null>(null);
  const [isAllSessionsRevokeOpen, setIsAllSessionsRevokeOpen] = useState(false);
  const [sessionPage, setSessionPage] = useState(1);
  const [openSettingsSections, setOpenSettingsSections] = useState<Record<SettingsSectionId, boolean>>({
    profile: false,
    password: user.mustChangePassword,
    sessions: false,
  });

  const isApiSession = Boolean(token && !token.startsWith("demo-"));
  const statusClassName =
    statusTone === "error" ? "form-error" : statusTone === "success" ? "form-success" : "form-info";

  const showStatus = useCallback((message: string, tone: StatusTone) => {
    setStatusMessage(message);
    setStatusTone(tone);
  }, []);

  function toggleSettingsSection(sectionId: SettingsSectionId) {
    setOpenSettingsSections((current) => ({
      ...current,
      [sectionId]: !current[sectionId],
    }));
  }

  const loadIdentityData = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    setIsLoadingSettings(true);
    try {
      const sessionResult = await api.listSessions(token);
      setSessions(sessionResult);
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.loadFailed")), "error");
    } finally {
      setIsLoadingSettings(false);
    }
  }, [language, showStatus, t, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadIdentityData();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadIdentityData]);

  useEffect(() => {
    if (!verificationResendAvailableAt) {
      return;
    }

    function syncCooldown() {
      const availableAt = Date.parse(verificationResendAvailableAt ?? "");
      if (Number.isNaN(availableAt)) {
        setVerificationResendSecondsLeft(0);
        return;
      }

      setVerificationResendSecondsLeft(Math.max(0, Math.ceil((availableAt - Date.now()) / 1000)));
    }

    syncCooldown();
    const interval = window.setInterval(syncCooldown, 1000);
    return () => window.clearInterval(interval);
  }, [verificationResendAvailableAt]);

  async function saveProfile() {
    if (!token) {
      return;
    }

    setIsSavingProfile(true);
    try {
      const updatedUser = await api.updateProfile(token, {
        displayName: profileDisplayName,
        email: profileEmail,
      });
      onUserUpdated(updatedUser);
      setProfileDisplayName(updatedUser.displayName);
      setProfileEmail(updatedUser.email);
      if (!updatedUser.isEmailVerified) {
        setDemoCode(null);
        setVerificationExpiresAt(null);
        setVerificationResendAvailableAt(null);
        setVerificationResendSecondsLeft(0);
        setVerificationCode("");
        setIsEmailVerificationOpen(false);
      }
      showStatus(t("settings.profileSaved"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.profileFailed")), "error");
    } finally {
      setIsSavingProfile(false);
    }
  }

  async function changePassword() {
    if (!token) {
      return;
    }

    setIsChangingPassword(true);
    try {
      const updatedUser = await api.changePassword(token, {
        currentPassword,
        newPassword,
      });
      onUserUpdated(updatedUser);
      setCurrentPassword("");
      setNewPassword("");
      showStatus(t("settings.passwordChanged"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.passwordFailed")), "error");
    } finally {
      setIsChangingPassword(false);
    }
  }

  async function requestVerification() {
    if (!token) {
      return;
    }

    setIsRequestingVerification(true);
    try {
      const response = await api.startEmailVerification(token);
      setDemoCode(response.demoCode || null);
      setVerificationExpiresAt(response.expiresAt);
      setVerificationResendAvailableAt(new Date(Date.now() + emailVerificationResendCooldownMs).toISOString());
      showStatus(t("settings.verificationCodeReady"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.verificationFailed")), "error");
    } finally {
      setIsRequestingVerification(false);
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
      setVerificationExpiresAt(null);
      setVerificationResendAvailableAt(null);
      setVerificationResendSecondsLeft(0);
      setIsEmailVerificationOpen(false);
      showStatus(t("settings.emailVerified"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.verificationFailed")), "error");
    }
  }

  function requestOwnSessionRevoke(session: UserSession) {
    setPendingOwnSessionRevoke({
      userId: user.id,
      sessionId: session.id,
      displayName: user.displayName,
      username: user.username,
      expiresAt: session.expiresAt,
      isCurrent: session.isCurrent,
    });
  }

  async function confirmOwnSessionRevoke() {
    if (!pendingOwnSessionRevoke) {
      return;
    }

    const revoke = pendingOwnSessionRevoke;
    setPendingOwnSessionRevoke(null);
    await revokeSession(revoke.sessionId, revoke.isCurrent);
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
      showStatus(t("settings.sessionRevoked"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.sessionRevokeFailed")), "error");
    }
  }

  async function confirmAllSessionsRevoke() {
    setIsAllSessionsRevokeOpen(false);
    await revokeAllSessions();
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
      showStatus(t("settings.allSessionsRevoked"), "success");
    } catch (error) {
      showStatus(localizeApiError(error, language, t("settings.sessionRevokeFailed")), "error");
    }
  }

  const isVerificationCooldownActive = verificationResendSecondsLeft > 0;
  const verificationCooldownLabel = formatCountdown(verificationResendSecondsLeft);
  const sessionPageSize = 4;
  const sessionTotalPages = Math.max(1, Math.ceil(sessions.length / sessionPageSize));
  const currentSessionPage = Math.min(sessionPage, sessionTotalPages);
  const visibleSessions = sessions.slice((currentSessionPage - 1) * sessionPageSize, currentSessionPage * sessionPageSize);

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
          <strong className={user.isEmailVerified ? "verified-status" : undefined}>
            {user.isEmailVerified ? (
              <>
                <ShieldCheck size={18} />
                {t("settings.verified")}
              </>
            ) : (
              t("settings.notVerified")
            )}
          </strong>
          {!user.isEmailVerified ? (
            <div className="settings-card-action">
              <button
                className="text-link-button"
                type="button"
                disabled={!isApiSession}
                onClick={() => setIsEmailVerificationOpen((isOpen) => !isOpen)}
              >
                {t("settings.verifyEmail")}
              </button>
            </div>
          ) : null}
        </article>
        <article className="settings-row">
          <span>{t("settings.session")}</span>
          <strong>{formatSessionExpiry(expiresAt, language)}</strong>
        </article>
      </div>

      {!user.isEmailVerified && isEmailVerificationOpen ? (
        <section className="identity-section email-verification-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("settings.emailVerificationTitle")}</span>
              <h3>{t("settings.verifyEmail")}</h3>
            </div>
            <ShieldCheck size={22} />
          </div>
          <div className="inline-verification-form">
            <button
              className="secondary-button"
              type="button"
              disabled={!isApiSession || isRequestingVerification || isVerificationCooldownActive}
              onClick={requestVerification}
            >
              {isRequestingVerification ? <span className="button-spinner" aria-hidden="true" /> : null}
              {isVerificationCooldownActive
                ? t("settings.resendAvailableIn", { value: verificationCooldownLabel })
                : verificationExpiresAt
                  ? t("settings.resendVerificationCode")
                  : t("settings.sendVerificationCode")}
            </button>
            <small>
              {verificationExpiresAt
                ? t("settings.verificationValidUntil", {
                    value: formatSessionExpiry(verificationExpiresAt, language),
                  })
                : t("settings.verificationValidityHint")}
            </small>
            {verificationExpiresAt ? (
              <small>
                {isVerificationCooldownActive
                  ? t("settings.resendCooldownHint", { value: verificationCooldownLabel })
                  : t("settings.resendReadyHint")}
              </small>
            ) : null}
            <input
              value={verificationCode}
              onChange={(event) => setVerificationCode(event.target.value)}
              placeholder={t("settings.verificationCode")}
            />
            <button className="primary-button" type="button" disabled={!isApiSession} onClick={confirmVerification}>
              {t("settings.verifyEmail")}
            </button>
            {demoCode ? <small>{t("settings.demoVerificationCode", { code: demoCode })}</small> : null}
          </div>
        </section>
      ) : null}

      {statusMessage ? <div className={statusClassName}>{statusMessage}</div> : null}

      {user.mustChangePassword ? (
        <section className="identity-section urgent-identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("settings.mustChangePasswordTitle")}</span>
              <h3>{t("settings.passwordTitle")}</h3>
            </div>
            <AlertTriangle size={22} />
          </div>
          <p className="helper-copy">{t("settings.mustChangePasswordDescription")}</p>
        </section>
      ) : null}

      <div className="settings-disclosure-stack">
        <DisclosureSection
          eyebrow={t("settings.profile")}
          icon={<Save size={20} />}
          isOpen={openSettingsSections.profile}
          onToggle={() => toggleSettingsSection("profile")}
          title={t("settings.profileTitle")}
          description={t("settings.profileDescription")}
        >
          <div className="compact-form">
            <input
              value={profileDisplayName}
              onChange={(event) => setProfileDisplayName(event.target.value)}
              placeholder={t("login.displayName")}
            />
            <input
              value={profileEmail}
              onChange={(event) => setProfileEmail(event.target.value)}
              placeholder={t("login.email")}
              type="email"
            />
            <button className="primary-button" type="button" disabled={!isApiSession || isSavingProfile} onClick={saveProfile}>
              {isSavingProfile ? t("common.saving") : t("settings.saveProfile")}
            </button>
          </div>
        </DisclosureSection>

        <DisclosureSection
          eyebrow={t("settings.auth")}
          icon={<KeyRound size={20} />}
          isOpen={openSettingsSections.password}
          onToggle={() => toggleSettingsSection("password")}
          title={t("settings.passwordTitle")}
          description={t("settings.passwordDescription")}
          className={user.mustChangePassword ? "urgent-identity-section" : undefined}
        >
          <div className="compact-form">
            <input
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              placeholder={t("settings.currentPassword")}
              type="password"
            />
            <input
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              placeholder={t("settings.newPassword")}
              type="password"
            />
            <button
              className={user.mustChangePassword ? "primary-button danger-button" : "primary-button"}
              type="button"
              disabled={!isApiSession || isChangingPassword || !currentPassword || !newPassword}
              onClick={changePassword}
            >
              {isChangingPassword ? t("common.saving") : t("settings.changePassword")}
            </button>
          </div>
        </DisclosureSection>

        <DisclosureSection
          eyebrow={t("settings.sessions")}
          icon={<ShieldCheck size={22} />}
          isOpen={openSettingsSections.sessions}
          onToggle={() => toggleSettingsSection("sessions")}
          title={t("settings.sessionsTitle")}
          description={t("settings.sessionsDescription")}
        >
        {isLoadingSettings ? <p className="status-line">{t("common.loading")}</p> : null}
        <div className="session-list">
          {visibleSessions.map((session) => (
            <article className="settings-row session-row" key={session.id}>
              <div className="stacked-summary">
                <span>{session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}</span>
                <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                <small>
                  {session.lastSeenAt
                    ? t("settings.lastSeen", { value: formatSessionExpiry(session.lastSeenAt, language) })
                    : t("settings.notSeenYet")}
                </small>
                <small>{t("settings.createdAt", { value: formatSessionExpiry(session.createdAt, language) })}</small>
                <small>{t("settings.device", { value: summarizeUserAgent(session.userAgent, language) })}</small>
                <small>{t("settings.ipAddress", { value: formatIpAddress(session.ipAddress, language) })}</small>
              </div>
              <button
                className="secondary-button danger-button"
                type="button"
                disabled={!isApiSession}
                onClick={() => requestOwnSessionRevoke(session)}
              >
                {t("settings.revokeSession")}
              </button>
            </article>
          ))}
          {!sessions.length && !isLoadingSettings ? <p className="status-line">{t("settings.noSessions")}</p> : null}
        </div>
        {sessions.length > sessionPageSize ? (
          <PaginationControls
            currentPage={currentSessionPage}
            language={language}
            onNext={() => setSessionPage((value) => Math.min(value + 1, sessionTotalPages))}
            onPageChange={setSessionPage}
            onPrevious={() => setSessionPage((value) => Math.max(value - 1, 1))}
            totalPages={sessionTotalPages}
          />
        ) : null}
        <div className="session-danger-action">
          <button
            className="danger-button strong-danger-button"
            type="button"
            disabled={!isApiSession}
            onClick={() => setIsAllSessionsRevokeOpen(true)}
          >
            {t("settings.revokeAllSessions")}
          </button>
        </div>
        </DisclosureSection>
      </div>
      {pendingOwnSessionRevoke ? (
        <OwnSessionRevokeDialog
          revoke={pendingOwnSessionRevoke}
          language={language}
          onCancel={() => setPendingOwnSessionRevoke(null)}
          onConfirm={confirmOwnSessionRevoke}
        />
      ) : null}
      {isAllSessionsRevokeOpen ? (
        <AllSessionsRevokeDialog
          sessionCount={sessions.length}
          language={language}
          onCancel={() => setIsAllSessionsRevokeOpen(false)}
          onConfirm={confirmAllSessionsRevoke}
        />
      ) : null}
    </section>
  );
}

function DisclosureSection({
  children,
  className,
  description,
  eyebrow,
  icon,
  isOpen,
  onToggle,
  title,
}: {
  children: ReactNode;
  className?: string;
  description: string;
  eyebrow: string;
  icon: ReactNode;
  isOpen: boolean;
  onToggle: () => void;
  title: string;
}) {
  return (
    <section className={["identity-section disclosure-section", className].filter(Boolean).join(" ")}>
      <button className="disclosure-trigger" type="button" aria-expanded={isOpen} onClick={onToggle}>
        <div className="disclosure-title-group">
          <span className="disclosure-leading-icon">{icon}</span>
          <div>
            <span className="eyebrow">{eyebrow}</span>
            <h3>{title}</h3>
            <p className="helper-copy">{description}</p>
          </div>
        </div>
        <span className="disclosure-icons" aria-hidden="true">
          <ChevronDown className={isOpen ? "disclosure-chevron open" : "disclosure-chevron"} size={18} />
        </span>
      </button>
      {isOpen ? <div className="disclosure-content">{children}</div> : null}
    </section>
  );
}

function UsersAndRolesView({
  activeUser,
  language,
  token,
}: {
  activeUser: User;
  language: Language;
  token: string | null;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [totalUsers, setTotalUsers] = useState(0);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<UserStatus | "All">("PendingApproval");
  const [accessDraft, setAccessDraft] = useState<AccessDraft | null>(null);
  const [pendingAccessChange, setPendingAccessChange] = useState<PendingAccessChange | null>(null);
  const [selectedUserSessions, setSelectedUserSessions] = useState<UserSession[]>([]);
  const [pendingSessionRevoke, setPendingSessionRevoke] = useState<PendingSessionRevoke | null>(null);
  const [pendingUserDelete, setPendingUserDelete] = useState<PendingUserDelete | null>(null);
  const [createUserDraft, setCreateUserDraft] = useState({
    username: "",
    displayName: "",
    email: "",
    role: "User" as Role,
    status: "Active" as UserStatus,
    temporaryPassword: "",
  });
  const [usesCustomTemporaryPassword, setUsesCustomTemporaryPassword] = useState(false);
  const [page, setPage] = useState(1);
  const [detailSessionPage, setDetailSessionPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [messageTone, setMessageTone] = useState<StatusTone>("info");
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingUserLogs, setIsLoadingUserLogs] = useState(false);
  const [isLoadingUserSessions, setIsLoadingUserSessions] = useState(false);
  const [isCreatingUser, setIsCreatingUser] = useState(false);
  const [isCreateUserOpen, setIsCreateUserOpen] = useState(false);
  const pageSize = 4;
  const userPageCache = useRef(new Map<string, PagedResult<UserAdmin>>());
  const userMessageClassName =
    messageTone === "error" ? "form-error" : messageTone === "success" ? "form-success" : "form-info";

  const showUserMessage = useCallback((nextMessage: string | null, tone: StatusTone = "info") => {
    setMessage(nextMessage);
    setMessageTone(tone);
  }, []);

  const loadUsers = useCallback(async (options: { force?: boolean; manual?: boolean } = {}) => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    const query = searchQuery.trim();
    const cacheKey = getUserPageCacheKey(query, statusFilter, page, pageSize);
    const cachedPage = options.force ? undefined : userPageCache.current.get(cacheKey);
    if (cachedPage) {
      setUsers(cachedPage.items ?? []);
      setTotalUsers(cachedPage.totalCount ?? 0);
    }

    setIsLoading(!cachedPage);
    try {
      const userResult = await api.listUsers(token, {
        query,
        status: statusFilter,
        page,
        pageSize,
      });
      userPageCache.current.set(cacheKey, userResult);
      setUsers(userResult.items ?? []);
      setTotalUsers(userResult.totalCount ?? 0);
      prefetchAdjacentUserPages(token, userPageCache.current, query, statusFilter, page, pageSize, userResult.totalCount ?? 0);
      if (options.manual) {
        showUserMessage(t("common.refreshed"), "success");
      }
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
    } finally {
      setIsLoading(false);
    }
  }, [language, page, pageSize, searchQuery, showUserMessage, statusFilter, t, token]);

  function refreshUsers() {
    userPageCache.current.clear();
    void loadUsers({ force: true, manual: true });
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsers();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadUsers]);

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleUsers = users;
  const selectedUser = selectedUserId ? users.find((managedUser) => managedUser.id === selectedUserId) ?? null : null;
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
        .sort(sortAuditNewestFirst)
    : [];
  const activeSessionCount = selectedUserSessions.length;
  const detailSessionPageSize = 4;
  const detailSessionTotalPages = Math.max(1, Math.ceil(selectedUserSessions.length / detailSessionPageSize));
  const currentDetailSessionPage = Math.min(detailSessionPage, detailSessionTotalPages);
  const visibleSelectedUserSessions = selectedUserSessions.slice(
    (currentDetailSessionPage - 1) * detailSessionPageSize,
    currentDetailSessionPage * detailSessionPageSize,
  );
  const isSelectedUserOnline = activeSessionCount > 0;
  const hasDraftChanges =
    !!selectedUser &&
    !!accessDraft &&
    accessDraft.userId === selectedUser.id &&
    (accessDraft.role !== selectedUser.role || accessDraft.status !== selectedUser.status);

  const loadSelectedUserSessions = useCallback(
    async (userId: string) => {
      if (!token || token.startsWith("demo-")) {
        setSelectedUserSessions([]);
        return;
      }

      setIsLoadingUserSessions(true);
      try {
        const result = await api.listUserSessions(token, userId);
        setSelectedUserSessions(result);
      } catch (error) {
        showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
        setSelectedUserSessions([]);
      } finally {
        setIsLoadingUserSessions(false);
      }
    },
    [language, showUserMessage, t, token],
  );

  const loadSelectedUserLogs = useCallback(
    async (managedUser: UserAdmin) => {
      if (!token || token.startsWith("demo-")) {
        setLogs([]);
        return;
      }

      setIsLoadingUserLogs(true);
      try {
        const result = await api.listSystemAuditLogs(token, {
          query: managedUser.username,
          page: 1,
          pageSize: 50,
        });
        setLogs(result.items ?? []);
      } catch (error) {
        showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
        setLogs([]);
      } finally {
        setIsLoadingUserLogs(false);
      }
    },
    [language, showUserMessage, t, token],
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (!selectedUser) {
        setAccessDraft(null);
        setSelectedUserSessions([]);
        setLogs([]);
        return;
      }

      setAccessDraft({ userId: selectedUser.id, role: selectedUser.role, status: selectedUser.status });
      setDetailSessionPage(1);
      void loadSelectedUserSessions(selectedUser.id);
      void loadSelectedUserLogs(selectedUser);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadSelectedUserLogs, loadSelectedUserSessions, selectedUser]);

  async function updateUserAccess(userId: string, role: Role, status: UserStatus) {
    if (!token) {
      return;
    }

    try {
      await api.updateUserAccess(token, userId, role, status);
      await loadUsers();
      showUserMessage(t("settings.userAccessUpdated"), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.userAccessFailed")), "error");
    }
  }

  async function createUser() {
    if (!token) {
      return;
    }

    const payload = {
      ...createUserDraft,
      temporaryPassword: usesCustomTemporaryPassword ? createUserDraft.temporaryPassword : "",
    };

    setIsCreatingUser(true);
    try {
      const createdUser = await api.createUser(token, payload);
      setCreateUserDraft({
        username: "",
        displayName: "",
        email: "",
        role: "User",
        status: "Active",
        temporaryPassword: "",
      });
      setUsesCustomTemporaryPassword(false);
      await loadUsers();
      setSelectedUserId(createdUser.id);
      showUserMessage(t("users.userCreated", { username: createdUser.username }), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("users.userCreateFailed")), "error");
    } finally {
      setIsCreatingUser(false);
    }
  }

  function requestUserAccessChange(managedUser: UserAdmin, role: Role, status: UserStatus) {
    if (managedUser.role === role && managedUser.status === status) {
      return;
    }

    showUserMessage(null);
    setPendingAccessChange({
      userId: managedUser.id,
      displayName: managedUser.displayName,
      username: managedUser.username,
      fromRole: managedUser.role,
      toRole: role,
      fromStatus: managedUser.status,
      toStatus: status,
    });
  }

  function requestDraftAccessChange() {
    if (!selectedUser || !accessDraft || !hasDraftChanges) {
      return;
    }

    requestUserAccessChange(selectedUser, accessDraft.role, accessDraft.status);
  }

  async function confirmUserAccessChange() {
    if (!pendingAccessChange) {
      return;
    }

    const change = pendingAccessChange;
    setPendingAccessChange(null);
    await updateUserAccess(change.userId, change.toRole, change.toStatus);
  }

  function requestSessionRevoke(session: UserSession) {
    if (!selectedUser) {
      return;
    }

    setPendingSessionRevoke({
      userId: selectedUser.id,
      sessionId: session.id,
      displayName: selectedUser.displayName,
      username: selectedUser.username,
      expiresAt: session.expiresAt,
      isCurrent: session.isCurrent && selectedUser.id === activeUser.id,
    });
  }

  async function confirmSessionRevoke() {
    if (!pendingSessionRevoke || !token) {
      return;
    }

    const revoke = pendingSessionRevoke;
    setPendingSessionRevoke(null);
    try {
      await api.revokeUserSession(token, revoke.userId, revoke.sessionId);
      await loadSelectedUserSessions(revoke.userId);
      await loadUsers();
      showUserMessage(t("settings.sessionRevoked"), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.sessionRevokeFailed")), "error");
    }
  }

  function requestUserDelete(managedUser: UserAdmin) {
    setPendingUserDelete({
      userId: managedUser.id,
      displayName: managedUser.displayName,
      username: managedUser.username,
    });
  }

  async function confirmUserDelete() {
    if (!pendingUserDelete || !token) {
      return;
    }

    const deletion = pendingUserDelete;
    setPendingUserDelete(null);
    try {
      await api.deleteUser(token, deletion.userId);
      setSelectedUserId(null);
      setSelectedUserSessions([]);
      await loadUsers();
      showUserMessage(t("users.userDeleted", { username: deletion.username }), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("users.userDeleteFailed")), "error");
    }
  }

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("users.eyebrow")}</span>
          <h2>{t("users.title")}</h2>
        </div>
        <div className="section-heading-actions">
          <p>{t("users.description")}</p>
          <button
            className="secondary-button refresh-button"
            disabled={isLoading}
            type="button"
            onClick={refreshUsers}
          >
            <RefreshCw className={isLoading ? "spin-icon" : undefined} size={17} />
            {isLoading ? t("common.refreshing") : t("common.refresh")}
          </button>
        </div>
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
          <label className="filter-select-field">
            <Filter size={16} />
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
          </label>
        </div>
        {message ? <div className={userMessageClassName}>{message}</div> : null}
        {isLoading ? <p className="status-line">{t("common.loading")}</p> : null}
      </div>

      <div className="management-layout">
        <div className="management-left-column">
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
            onPageChange={setPage}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>
        <section className="identity-section user-create-disclosure">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.createEyebrow")}</span>
              <h3>{t("users.createTitle")}</h3>
            </div>
            <button
              className={isCreateUserOpen ? "secondary-button" : "primary-button"}
              type="button"
              onClick={() => setIsCreateUserOpen((isOpen) => !isOpen)}
            >
              <UserPlus size={17} />
              {isCreateUserOpen ? t("common.close") : t("users.createUser")}
            </button>
          </div>
          {isCreateUserOpen ? (
            <div className="admin-create-panel">
              <p className="helper-copy">{t("users.createDescription")}</p>
              <div className="admin-create-grid">
                <input
                  value={createUserDraft.username}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, username: event.target.value }))}
                  placeholder={t("login.username")}
                />
                <input
                  value={createUserDraft.displayName}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, displayName: event.target.value }))}
                  placeholder={t("login.displayName")}
                />
                <input
                  value={createUserDraft.email}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, email: event.target.value }))}
                  placeholder={t("login.email")}
                  type="email"
                />
                {usesCustomTemporaryPassword ? (
                  <input
                    value={createUserDraft.temporaryPassword}
                    onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, temporaryPassword: event.target.value }))}
                    placeholder={t("users.temporaryPassword")}
                    type="password"
                  />
                ) : (
                  <div className="generated-password-placeholder">
                    <Sparkles size={16} />
                    <span>{t("users.autoTemporaryPassword")}</span>
                  </div>
                )}
                <select
                  value={createUserDraft.role}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, role: event.target.value as Role }))}
                >
                  <option value="Admin">Admin</option>
                  <option value="User">User</option>
                  <option value="Approver">Approver</option>
                </select>
                <select
                  value={createUserDraft.status}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, status: event.target.value as UserStatus }))}
                >
                  <option value="PendingApproval">{t("settings.statusPending")}</option>
                  <option value="Active">{t("settings.statusActive")}</option>
                  <option value="Rejected">{t("settings.statusRejected")}</option>
                </select>
                <small className="admin-create-note">
                  {usesCustomTemporaryPassword ? t("users.customTemporaryPasswordMailNote") : t("users.temporaryPasswordMailNote")}
                </small>
              </div>
              <div className="admin-create-actions">
                <label className="checkbox-line custom-password-toggle compact-password-toggle">
                  <input
                    checked={usesCustomTemporaryPassword}
                    onChange={(event) => {
                      setUsesCustomTemporaryPassword(event.target.checked);
                      if (!event.target.checked) {
                        setCreateUserDraft((draft) => ({ ...draft, temporaryPassword: "" }));
                      }
                    }}
                    type="checkbox"
                  />
                  <span>{t("users.useCustomTemporaryPassword")}</span>
                </label>
                <button className="primary-button" type="button" disabled={isCreatingUser} onClick={createUser}>
                  {isCreatingUser ? t("users.creatingUser") : t("users.createUser")}
                </button>
              </div>
            </div>
          ) : null}
        </section>
        </div>

        <section className={selectedUser ? "identity-section user-detail-panel detail-expanded" : "identity-section user-detail-panel detail-placeholder"}>
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.detailEyebrow")}</span>
              <h3>{selectedUser ? selectedUser.displayName : t("users.noSelection")}</h3>
            </div>
            {selectedUser ? (
              <button className="icon-button" type="button" onClick={() => setSelectedUserId(null)} title={t("common.close")}>
                <X size={18} />
              </button>
            ) : (
              <History size={22} />
            )}
          </div>
          {selectedUser ? (
            <div className="user-detail-content">
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
                <article className="settings-row">
                  <span>{t("users.onlineStatus")}</span>
                  <strong>{isSelectedUserOnline ? t("users.online") : t("users.offline")}</strong>
                  <small>{t("users.activeSessionCount", { count: activeSessionCount })}</small>
                </article>
                <article className="settings-row">
                  <span>{t("users.mustChangePassword")}</span>
                  <strong>{selectedUser.mustChangePassword ? t("common.yes") : t("common.no")}</strong>
                </article>
              </div>
              <div className="access-editor">
                <select
                  value={accessDraft?.role ?? selectedUser.role}
                  onChange={(event) =>
                    setAccessDraft({
                      userId: selectedUser.id,
                      role: event.target.value as Role,
                      status: accessDraft?.status ?? selectedUser.status,
                    })
                  }
                >
                  <option value="Admin">Admin</option>
                  <option value="User">User</option>
                  <option value="Approver">Approver</option>
                </select>
                <select
                  value={accessDraft?.status ?? selectedUser.status}
                  onChange={(event) =>
                    setAccessDraft({
                      userId: selectedUser.id,
                      role: accessDraft?.role ?? selectedUser.role,
                      status: event.target.value as UserStatus,
                    })
                  }
                >
                  <option value="PendingApproval">{t("settings.statusPending")}</option>
                  <option value="Active">{t("settings.statusActive")}</option>
                  <option value="Rejected">{t("settings.statusRejected")}</option>
                </select>
                <button
                  className="primary-button"
                  type="button"
                  disabled={!hasDraftChanges}
                  onClick={requestDraftAccessChange}
                >
                  {t("users.applyAccessChange")}
                </button>
              </div>
              <section className="identity-section nested-identity-section">
                <div className="section-toolbar">
                  <div>
                    <span className="eyebrow">{t("users.sessionsEyebrow")}</span>
                    <h3>{t("users.sessionsTitle")}</h3>
                  </div>
                  <ShieldCheck size={22} />
                </div>
                {isLoadingUserSessions ? <p className="status-line">{t("common.loading")}</p> : null}
                <div className="session-list">
                  {visibleSelectedUserSessions.map((session) => (
                    <article className="settings-row session-row" key={session.id}>
                      <div className="stacked-summary">
                        <span>{session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}</span>
                        <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                        <small>
                          {session.lastSeenAt
                            ? t("settings.lastSeen", { value: formatSessionExpiry(session.lastSeenAt, language) })
                            : t("settings.notSeenYet")}
                        </small>
                        <small>{t("settings.createdAt", { value: formatSessionExpiry(session.createdAt, language) })}</small>
                        <small>{t("settings.device", { value: summarizeUserAgent(session.userAgent, language) })}</small>
                        <small>{t("settings.ipAddress", { value: formatIpAddress(session.ipAddress, language) })}</small>
                      </div>
                      <button
                        className="secondary-button danger-button"
                        type="button"
                        disabled={session.isCurrent && selectedUser.id === activeUser.id}
                        onClick={() => requestSessionRevoke(session)}
                      >
                        {t("settings.revokeSession")}
                      </button>
                    </article>
                  ))}
                  {!selectedUserSessions.length && !isLoadingUserSessions ? (
                    <p className="status-line">{t("users.noActiveSessions")}</p>
                  ) : null}
                </div>
                {selectedUserSessions.length > detailSessionPageSize ? (
                  <PaginationControls
                    currentPage={currentDetailSessionPage}
                    language={language}
                    onNext={() => setDetailSessionPage((value) => Math.min(value + 1, detailSessionTotalPages))}
                    onPageChange={setDetailSessionPage}
                    onPrevious={() => setDetailSessionPage((value) => Math.max(value - 1, 1))}
                    totalPages={detailSessionTotalPages}
                  />
                ) : null}
              </section>
              {isLoadingUserLogs ? <p className="status-line">{t("common.loading")}</p> : null}
              <SystemAuditTimeline logs={selectedUserLogs} language={language} emptyText={t("users.noUserLogs")} />
              <div className="detail-danger-action">
                <button
                  className="danger-button strong-danger-button"
                  type="button"
                  disabled={selectedUser.id === activeUser.id}
                  onClick={() => requestUserDelete(selectedUser)}
                >
                  {t("users.deleteUser")}
                </button>
              </div>
            </div>
          ) : (
            <p className="status-line">{t("users.noSelectionHelp")}</p>
          )}
        </section>
      </div>
      {pendingAccessChange ? (
        <AccessChangeDialog
          change={pendingAccessChange}
          language={language}
          onCancel={() => setPendingAccessChange(null)}
          onConfirm={confirmUserAccessChange}
        />
      ) : null}
      {pendingSessionRevoke ? (
        <SessionRevokeDialog
          revoke={pendingSessionRevoke}
          language={language}
          onCancel={() => setPendingSessionRevoke(null)}
          onConfirm={confirmSessionRevoke}
        />
      ) : null}
      {pendingUserDelete ? (
        <UserDeleteDialog
          deletion={pendingUserDelete}
          language={language}
          onCancel={() => setPendingUserDelete(null)}
          onConfirm={confirmUserDelete}
        />
      ) : null}
    </section>
  );
}

function OwnSessionRevokeDialog({
  revoke,
  language,
  onCancel,
  onConfirm,
}: {
  revoke: PendingSessionRevoke;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("settings.sessionRevokeEyebrow")}</span>
            <strong>{t(revoke.isCurrent ? "settings.currentSessionRevokeTitle" : "settings.sessionRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t(revoke.isCurrent ? "settings.currentSessionRevokeDescription" : "settings.sessionRevokeDescription", {
            expiresAt: formatSessionExpiry(revoke.expiresAt, language),
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("settings.revokeSession")}
          </button>
        </div>
      </div>
    </div>
  );
}

function AllSessionsRevokeDialog({
  sessionCount,
  language,
  onCancel,
  onConfirm,
}: {
  sessionCount: number;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("settings.allSessionsRevokeEyebrow")}</span>
            <strong>{t("settings.allSessionsRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("settings.allSessionsRevokeDescription", {
            count: sessionCount,
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("settings.revokeAllSessions")}
          </button>
        </div>
      </div>
    </div>
  );
}

function AccessChangeDialog({
  change,
  language,
  onCancel,
  onConfirm,
}: {
  change: PendingAccessChange;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const isHighRisk = change.toRole === "Admin" || change.fromRole === "Admin" || change.toStatus !== change.fromStatus;

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.accessConfirmEyebrow")}</span>
            <strong>{t(isHighRisk ? "users.accessConfirmTitleCritical" : "users.accessConfirmTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("users.accessConfirmDescription", {
            displayName: change.displayName,
            username: change.username,
          })}
        </p>
        <div className="access-confirm-grid">
          <article className="settings-row">
            <span>{t("session.role")}</span>
            <strong>
              {roleLabel(language, change.fromRole)} -&gt; {roleLabel(language, change.toRole)}
            </strong>
          </article>
          <article className="settings-row">
            <span>{t("settings.status")}</span>
            <strong>
              {userStatusLabel(language, change.fromStatus)} -&gt; {userStatusLabel(language, change.toStatus)}
            </strong>
          </article>
        </div>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("users.confirmAccessChange")}
          </button>
        </div>
      </div>
    </div>
  );
}

function SessionRevokeDialog({
  revoke,
  language,
  onCancel,
  onConfirm,
}: {
  revoke: PendingSessionRevoke;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.sessionRevokeEyebrow")}</span>
            <strong>{t("users.sessionRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("users.sessionRevokeDescription", {
            displayName: revoke.displayName,
            username: revoke.username,
            expiresAt: formatSessionExpiry(revoke.expiresAt, language),
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("users.confirmSessionRevoke")}
          </button>
        </div>
      </div>
    </div>
  );
}

function UserDeleteDialog({
  deletion,
  language,
  onCancel,
  onConfirm,
}: {
  deletion: PendingUserDelete;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.deleteConfirmEyebrow")}</span>
            <strong>{t("users.deleteConfirmTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("users.deleteConfirmDescription", {
            displayName: deletion.displayName,
            username: deletion.username,
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("users.confirmDeleteUser")}
          </button>
        </div>
      </div>
    </div>
  );
}

function SystemLogsView({ language, token }: { language: Language; token: string | null }) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [totalLogs, setTotalLogs] = useState(0);
  const [categoryCounts, setCategoryCounts] = useState<Record<AuditCategory, number | null>>({
    all: null,
    identity: null,
    access: null,
    forms: null,
    processes: null,
    tasks: null,
  });
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<AuditCategory>("all");
  const [selectedHistory, setSelectedHistory] = useState<SelectedAuditHistory | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const pageSize = 5;
  const trimmedQuery = searchQuery.trim().toLowerCase();
  const auditPageCache = useRef(new Map<string, PagedResult<SystemAuditLog>>());
  const shouldQueryLogs = trimmedQuery.length >= 2 || selectedCategory !== "all";

  const loadAuditCounts = useCallback(
    async (options: { force?: boolean } = {}) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      try {
        const counts = await api.listSystemAuditCounts(token, searchQuery.trim());
        setCategoryCounts({
          all: counts.all,
          identity: counts.identity,
          access: counts.access,
          forms: counts.forms,
          processes: counts.processes,
          tasks: counts.tasks,
        });
      } catch {
        if (options.force) {
          setCategoryCounts({
            all: null,
            identity: null,
            access: null,
            forms: null,
            processes: null,
            tasks: null,
          });
        }
      }
    },
    [searchQuery, token],
  );

  const loadLogs = useCallback(
    async (options: { force?: boolean; manual?: boolean } = {}) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      if (!shouldQueryLogs) {
        setLogs([]);
        setTotalLogs(0);
        setSelectedHistory(null);
        return;
      }

      const query = searchQuery.trim();
      const cacheKey = getAuditPageCacheKey(query, selectedCategory, page, pageSize);
      const cachedPage = options.force ? undefined : auditPageCache.current.get(cacheKey);
      if (cachedPage) {
        setLogs(cachedPage.items ?? []);
        setTotalLogs(cachedPage.totalCount ?? 0);
      }

      setIsLoading(!cachedPage);
      try {
        const auditResult = await api.listSystemAuditLogs(token, {
          query,
          category: selectedCategory,
          page,
          pageSize,
        });
        auditPageCache.current.set(cacheKey, auditResult);
        setLogs(auditResult.items ?? []);
        setTotalLogs(auditResult.totalCount ?? 0);
        prefetchAdjacentAuditPages(
          token,
          auditPageCache.current,
          query,
          selectedCategory,
          page,
          pageSize,
          auditResult.totalCount ?? 0,
        );
        if (options.manual) {
          void loadAuditCounts({ force: true });
        }
      } finally {
        setIsLoading(false);
      }
    },
    [loadAuditCounts, page, pageSize, searchQuery, selectedCategory, shouldQueryLogs, token],
  );

  function refreshLogs() {
    auditPageCache.current.clear();
    void loadLogs({ force: true, manual: true });
    void loadAuditCounts({ force: true });
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadLogs();
      void loadAuditCounts();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadAuditCounts, loadLogs]);
  const totalPages = Math.max(1, Math.ceil(totalLogs / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = shouldQueryLogs ? logs : [];
  const selectedLog = selectedHistory ? logs.find((log) => log.id === selectedHistory.logId) ?? null : null;
  const selectedLogCategory = selectedLog ? getAuditCategory(selectedLog) : null;
  const selectedHistoryTitle =
    selectedLog && selectedHistory
      ? getAuditHistoryTitle(selectedLog, selectedHistory.mode, language)
      : t("logs.noSelection");
  const historyFilterOptions = useMemo(() => {
    if (!selectedLog) {
      return [];
    }

    const options: Array<{ mode: AuditHistoryMode; label: string }> = [
      { mode: "related", label: t("logs.historyFilter.related") },
      { mode: "actor", label: t("logs.historyFilter.actor", { value: selectedLog.actorUsername }) },
    ];

    if (selectedLog.entityType === "User" && selectedLog.entityId) {
      options.push({
        mode: "target",
        label: t("logs.historyFilter.target", { value: getAuditTargetLabel(selectedLog) }),
      });
    }

    return options;
  }, [selectedLog, t]);
  const relatedLogs = useMemo(() => {
    if (!selectedLog || !selectedLogCategory || !selectedHistory) {
      return [];
    }

    return getFocusedAuditLogs(logs, selectedLog, selectedLogCategory, selectedHistory.mode);
  }, [logs, selectedHistory, selectedLog, selectedLogCategory]);
  const activeHistoryOptionIndex = Math.max(
    0,
    historyFilterOptions.findIndex((option) => option.mode === selectedHistory?.mode),
  );

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("logs.eyebrow")}</span>
          <h2>{t("logs.title")}</h2>
        </div>
        <div className="section-heading-actions">
          <p>{t("logs.description")}</p>
          <button
            className="secondary-button refresh-button"
            disabled={isLoading}
            type="button"
            onClick={refreshLogs}
          >
            <RefreshCw className={isLoading ? "spin-icon" : undefined} size={17} />
            {isLoading ? t("common.refreshing") : t("common.refresh")}
          </button>
        </div>
      </div>

      <section className="identity-section">
        <div className="audit-category-grid">
          {auditCategories.map((category) => (
            <button
              className={`audit-category-card ${selectedCategory === category ? "is-active" : ""}`}
              key={category}
              type="button"
              onClick={() => {
                setSelectedCategory(category);
                setPage(1);
                setSelectedHistory(null);
              }}
            >
              <span>{t(`logs.category.${category}` as TranslationKey)}</span>
              <strong>{categoryCounts[category] ?? "-"}</strong>
              <small>{t(`logs.categoryHelp.${category}` as TranslationKey)}</small>
            </button>
          ))}
        </div>
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
        {trimmedQuery.length < 2 && selectedCategory === "all" ? (
          <p className="helper-copy">{t("logs.searchFirst")}</p>
        ) : null}
        {trimmedQuery.length >= 2 || selectedCategory !== "all" ? (
          <p className="helper-copy">
            {t("logs.resultSummary", {
              count: totalLogs,
              category: t(`logs.category.${selectedCategory}` as TranslationKey),
            })}
          </p>
        ) : null}
      </section>

      <div className="management-layout">
        <section className="identity-section">
          <div className="system-audit-list">
            {visibleLogs.map((log) => (
              <article className="settings-row system-audit-row" key={log.id}>
                <div className="system-audit-content">
                  <div className="audit-label-row">
                    <span className={`audit-category-pill audit-category-${getAuditCategory(log)}`}>
                      {t(`logs.category.${getAuditCategory(log)}` as TranslationKey)}
                    </span>
                    <span>{formatAuditAction(log.action, language)}</span>
                  </div>
                  <strong>{log.description}</strong>
                  <small>
                    {log.actorDisplayName} / {log.actorUsername}
                  </small>
                </div>
                <div className="system-audit-meta">
                  <strong>{log.entityType}</strong>
                  <small>{formatApiDateTime(log.createdAt, language)}</small>
                  <button
                    className="secondary-button"
                    type="button"
                    onClick={() => setSelectedHistory({ logId: log.id, mode: "related" })}
                  >
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
            onPageChange={setPage}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>

        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("logs.relatedEyebrow")}</span>
              <h3>{selectedHistoryTitle}</h3>
            </div>
            <History size={22} />
          </div>
          {selectedLog && selectedHistory ? (
            <div
              className={`audit-radio-group audit-radio-active-${selectedHistory.mode}`}
              style={
                {
                  "--audit-option-count": historyFilterOptions.length,
                  "--audit-active-index": activeHistoryOptionIndex,
                } as CSSProperties
              }
              aria-label={t("logs.historyFilterLabel")}
              role="radiogroup"
            >
              {historyFilterOptions.map((option) => (
                <div className="audit-radio-item" key={option.mode}>
                  <input
                    checked={selectedHistory.mode === option.mode}
                    id={`audit-history-${selectedLog.id}-${option.mode}`}
                    name={`audit-history-${selectedLog.id}`}
                    onChange={() => setSelectedHistory({ logId: selectedLog.id, mode: option.mode })}
                    type="radio"
                    value={option.mode}
                  />
                  <label
                    className="audit-radio-option"
                    htmlFor={`audit-history-${selectedLog.id}-${option.mode}`}
                    role="radio"
                    aria-checked={selectedHistory.mode === option.mode}
                  >
                    {option.label}
                  </label>
                </div>
              ))}
              <div
                className="audit-radio-slider"
                aria-hidden="true"
              />
            </div>
          ) : null}
          <SystemAuditTimeline
            key={selectedHistory ? `${selectedHistory.logId}-${selectedHistory.mode}` : "no-related-log"}
            logs={relatedLogs}
            language={language}
            emptyText={t("logs.noRelated")}
            searchable
          />
        </section>
      </div>
    </section>
  );
}

function getFocusedAuditLogs(
  logs: SystemAuditLog[],
  selectedLog: SystemAuditLog,
  selectedLogCategory: Exclude<AuditCategory, "all">,
  mode: AuditHistoryMode,
) {
  return logs
    .filter((log) => {
      const sameActor = Boolean(selectedLog.actorUserId && log.actorUserId === selectedLog.actorUserId);
      const sameEntity = Boolean(
        selectedLog.entityId && log.entityId === selectedLog.entityId && log.entityType === selectedLog.entityType,
      );
      const targetUserAsActor = Boolean(
        selectedLog.entityType === "User" && selectedLog.entityId && log.actorUserId === selectedLog.entityId,
      );

      if (mode === "actor") {
        return sameActor;
      }

      if (mode === "target") {
        return sameEntity || targetUserAsActor;
      }

      if (selectedLog.entityId) {
        return sameActor && sameEntity;
      }

      return getAuditCategory(log) === selectedLogCategory && sameActor;
    })
    .sort(sortAuditNewestFirst);
}

function sortAuditNewestFirst(left: SystemAuditLog, right: SystemAuditLog) {
  return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
}

function getAuditHistoryTitle(
  log: SystemAuditLog,
  mode: AuditHistoryMode,
  language: Language,
) {
  if (mode === "actor") {
    return translate(language, "logs.actorHistoryTitle", { value: log.actorUsername });
  }

  if (mode === "target") {
    return translate(language, "logs.targetHistoryTitle", { value: getAuditTargetLabel(log) });
  }

  return translate(language, "logs.relatedHistoryTitle");
}

function getAuditTargetLabel(log: SystemAuditLog) {
  if (log.entityType === "User" && log.entityId) {
    return log.entityUsername ?? log.entityDisplayName ?? log.entityId;
  }

  const match = log.description.match(/(?:user)\s+'([^']+)'/i);
  return match?.[1] ?? log.entityId ?? log.entityType;
}

function getUserPageCacheKey(query: string, status: UserStatus | "All", page: number, pageSize: number) {
  return `${query.toLowerCase()}|${status}|${page}|${pageSize}`;
}

function getAuditPageCacheKey(query: string, category: AuditCategory, page: number, pageSize: number) {
  return `${query.toLowerCase()}|${category}|${page}|${pageSize}`;
}

function getAdjacentPages(page: number, pageSize: number, totalCount: number) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  return [page - 1, page + 1].filter((candidate) => candidate >= 1 && candidate <= totalPages);
}

function prefetchAdjacentUserPages(
  token: string,
  cache: Map<string, PagedResult<UserAdmin>>,
  query: string,
  status: UserStatus | "All",
  page: number,
  pageSize: number,
  totalCount: number,
) {
  for (const nextPage of getAdjacentPages(page, pageSize, totalCount)) {
    const cacheKey = getUserPageCacheKey(query, status, nextPage, pageSize);
    if (cache.has(cacheKey)) {
      continue;
    }

    void api
      .listUsers(token, { query, status, page: nextPage, pageSize })
      .then((result) => cache.set(cacheKey, result))
      .catch(() => undefined);
  }
}

function prefetchAdjacentAuditPages(
  token: string,
  cache: Map<string, PagedResult<SystemAuditLog>>,
  query: string,
  category: AuditCategory,
  page: number,
  pageSize: number,
  totalCount: number,
) {
  for (const nextPage of getAdjacentPages(page, pageSize, totalCount)) {
    const cacheKey = getAuditPageCacheKey(query, category, nextPage, pageSize);
    if (cache.has(cacheKey)) {
      continue;
    }

    void api
      .listSystemAuditLogs(token, { query, category, page: nextPage, pageSize })
      .then((result) => cache.set(cacheKey, result))
      .catch(() => undefined);
  }
}

function getAuditCategory(log: SystemAuditLog): Exclude<AuditCategory, "all"> {
  const identityActions = new Set([
    "Auth.AccountLocked",
    "Auth.EmailVerificationRequested",
    "Auth.EmailVerified",
    "Auth.LoginFailed",
    "Auth.LoginSucceeded",
    "Auth.Logout",
    "Auth.PasswordChanged",
    "Auth.RegisterRequested",
    "Auth.SessionRevoked",
    "Auth.TemporaryPasswordChanged",
    "User.ProfileAndEmailUpdated",
    "User.ProfileUpdated",
  ]);
  const accessActions = new Set(["Auth.AdminSessionRevoked", "User.AccessUpdated", "User.CreatedByAdmin", "User.DeletedByAdmin"]);

  if (identityActions.has(log.action)) {
    return "identity";
  }

  if (accessActions.has(log.action)) {
    return "access";
  }

  if (log.action.startsWith("FormDefinition.") || log.entityType === "FormDefinition") {
    return "forms";
  }

  if (log.action.startsWith("Task.") || log.entityType === "ProcessTask") {
    return "tasks";
  }

  if (log.action.startsWith("Process.") || log.entityType === "ProcessInstance") {
    return "processes";
  }

  return "identity";
}

function formatAuditAction(action: string, language: Language) {
  const key = `logs.action.${action}` as TranslationKey;
  const translated = translate(language, key);
  return translated === key ? action : translated;
}

function buildAuditSearchText(log: SystemAuditLog, language: Language) {
  return [
    log.actorDisplayName,
    log.actorUsername,
    log.action,
    formatAuditAction(log.action, language),
    log.entityType,
    log.entityId ?? "",
    log.description,
  ]
    .join(" ")
    .toLowerCase();
}

function SystemAuditTimeline({
  logs,
  language,
  emptyText,
  pageSize = 5,
  searchable = false,
}: {
  logs: SystemAuditLog[];
  language: Language;
  emptyText: string;
  pageSize?: number;
  searchable?: boolean;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [page, setPage] = useState(1);
  const [timelineQuery, setTimelineQuery] = useState("");
  const trimmedTimelineQuery = timelineQuery.trim().toLowerCase();
  const filteredLogs = useMemo(() => {
    if (!searchable || trimmedTimelineQuery.length < 2) {
      return logs;
    }

    return logs.filter((log) => buildAuditSearchText(log, language).includes(trimmedTimelineQuery));
  }, [language, logs, searchable, trimmedTimelineQuery]);
  const totalPages = Math.max(1, Math.ceil(filteredLogs.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = filteredLogs.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  if (logs.length === 0) {
    return (
      <div className="timeline-reveal">
        <p className="status-line">{emptyText}</p>
      </div>
    );
  }

  return (
    <div className="timeline-reveal">
      {searchable ? (
        <label className="search-field timeline-filter">
          <Search size={16} />
          <input
            value={timelineQuery}
            onChange={(event) => {
              setTimelineQuery(event.target.value);
              setPage(1);
            }}
            placeholder={t("logs.timelineSearchPlaceholder")}
          />
        </label>
      ) : null}
      <div className="system-audit-timeline">
        {visibleLogs.map((log) => (
          <article className="system-audit-event" key={log.id}>
            <span>{log.action}</span>
            <strong>{log.description}</strong>
            <small>
              {log.actorDisplayName} / {log.actorUsername} - {formatApiDateTime(log.createdAt, language)}
            </small>
          </article>
        ))}
        {!visibleLogs.length ? <p className="status-line">{t("logs.noRelated")}</p> : null}
      </div>
      {filteredLogs.length > pageSize ? (
        <PaginationControls
          currentPage={currentPage}
          language={language}
          onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
          onPageChange={setPage}
          onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
          totalPages={totalPages}
        />
      ) : null}
    </div>
  );
}

function PaginationControls({
  currentPage,
  language,
  onNext,
  onPageChange,
  onPrevious,
  totalPages,
}: {
  currentPage: number;
  language: Language;
  onNext: () => void;
  onPageChange: (page: number) => void;
  onPrevious: () => void;
  totalPages: number;
}) {
  const safeTotalPages = Math.max(1, totalPages);

  function applyDraftPage(input: HTMLInputElement) {
    const requestedPage = Number.parseInt(input.value, 10);
    if (Number.isNaN(requestedPage)) {
      input.value = String(currentPage);
      return;
    }

    const nextPage = Math.min(Math.max(requestedPage, 1), safeTotalPages);
    input.value = String(nextPage);
    if (nextPage !== currentPage) {
      onPageChange(nextPage);
    }
  }

  return (
    <div className="pagination-controls">
      <button className="icon-button" type="button" disabled={currentPage <= 1} onClick={onPrevious}>
        <ChevronLeft size={16} />
      </button>
      <label className="pagination-jump">
        <span>{translate(language, "common.page", { current: currentPage, total: safeTotalPages })}</span>
        <input
          aria-label={translate(language, "common.pageJump")}
          defaultValue={currentPage}
          inputMode="numeric"
          key={currentPage}
          min={1}
          max={safeTotalPages}
          type="number"
          onBlur={(event) => applyDraftPage(event.currentTarget)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.currentTarget.blur();
            }
          }}
        />
      </label>
      <button className="icon-button" type="button" disabled={currentPage >= totalPages} onClick={onNext}>
        <ChevronRight size={16} />
      </button>
    </div>
  );
}

function formatSessionExpiry(expiresAt: string | null, language: Language) {
  return formatApiDateTime(expiresAt, language);
}

function formatCountdown(totalSeconds: number) {
  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function summarizeUserAgent(userAgent: string | null | undefined, language: Language) {
  if (!userAgent) {
    return translate(language, "settings.unknownDevice");
  }

  const browser = userAgent.includes("Edg/")
    ? "Edge"
    : userAgent.includes("Chrome/")
      ? "Chrome"
      : userAgent.includes("Firefox/")
        ? "Firefox"
        : userAgent.includes("Safari/")
          ? "Safari"
          : "Browser";
  const os = userAgent.includes("Windows")
    ? "Windows"
    : userAgent.includes("Mac OS")
      ? "macOS"
      : userAgent.includes("Android")
        ? "Android"
        : userAgent.includes("iPhone") || userAgent.includes("iPad")
          ? "iOS"
          : "Device";

  return `${browser} / ${os}`;
}

function formatIpAddress(ipAddress: string | null | undefined, language: Language) {
  if (!ipAddress) {
    return translate(language, "settings.unknownIp");
  }

  if (ipAddress === "::1" || ipAddress === "127.0.0.1") {
    return translate(language, "settings.localhostIp");
  }

  if (ipAddress.startsWith("::ffff:")) {
    return ipAddress.replace("::ffff:", "");
  }

  return ipAddress;
}

function userStatusLabel(language: Language, status: UserStatus) {
  return translate(language, `userStatus.${status}` as TranslationKey);
}
