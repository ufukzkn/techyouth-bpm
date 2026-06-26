"use client";

import { LogOut, Moon, Sun } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { navItems, type ViewId } from "@/features/app-shell/navigation";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { LoginView } from "@/features/auth/LoginView";
import { FormDesignerDraft } from "@/features/form-designer/FormDesignerDraft";
import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";
import { ProcessBoardDraft } from "@/features/processes/ProcessBoardDraft";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { ProcessSummary, ProcessTask, User } from "@/lib/types";

export function AppShell() {
  const { user, token, expiresAt, theme, hasHydrated, expireSession, logout, toggleTheme } = useSessionStore();
  const [activeView, setActiveView] = useState<ViewId>("dashboard");

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  useEffect(() => {
    if (!hasHydrated || !token || !user || token.startsWith("demo-")) {
      return;
    }

    let ignore = false;
    let expiryTimer: number | undefined;
    const sessionToken = token;
    const expiresAtTime = expiresAt ? Date.parse(expiresAt) : null;

    if (expiresAtTime && expiresAtTime <= Date.now()) {
      expireSession("Oturum suresi doldu. Devam etmek icin tekrar giris yap.");
      return;
    }

    if (expiresAtTime) {
      expiryTimer = window.setTimeout(() => {
        expireSession("Oturum suresi doldu. Devam etmek icin tekrar giris yap.");
      }, expiresAtTime - Date.now());
    }

    async function verifySession() {
      try {
        await api.me(sessionToken);
      } catch (error) {
        if (ignore) {
          return;
        }

        if (error instanceof ApiError && error.statusCode === 401) {
          expireSession("Oturum dogrulanamadi. Lutfen tekrar giris yap.");
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
  }, [expiresAt, expireSession, hasHydrated, token, user]);

  useEffect(() => {
    function syncViewFromUrl() {
      const requestedView = new URLSearchParams(window.location.search).get("view");
      setActiveView(isViewId(requestedView) ? requestedView : "dashboard");
    }

    syncViewFromUrl();
    window.addEventListener("popstate", syncViewFromUrl);

    return () => window.removeEventListener("popstate", syncViewFromUrl);
  }, []);

  const changeView = useCallback((viewId: ViewId) => {
    setActiveView(viewId);

    const nextUrl = new URL(window.location.href);
    if (viewId === "dashboard") {
      nextUrl.searchParams.delete("view");
    } else {
      nextUrl.searchParams.set("view", viewId);
    }

    window.history.pushState(null, "", `${nextUrl.pathname}${nextUrl.search}`);
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
          <span className="eyebrow">Oturum</span>
          <h1>Hazirlaniyor</h1>
          <p>Kayitli oturum bilgisi kontrol ediliyor.</p>
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
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-symbol">
            <PrototypeLogo size={34} />
          </span>
          <div>
            <strong>TechYouth BPM</strong>
            <span>Wizard workspace</span>
          </div>
        </div>
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
                {item.label}
              </button>
            );
          })}
        </nav>
      </aside>

      <div className="main-area">
        <header className="topbar">
          <div>
            <span className="eyebrow">Aktif kullanici</span>
            <strong>{user.displayName}</strong>
          </div>
          <span className="role-pill">{user.role}</span>
          <button className="icon-button" onClick={toggleTheme} aria-label="Tema degistir" title="Tema degistir">
            {theme === "light" ? <Moon size={18} /> : <Sun size={18} />}
          </button>
          <button className="icon-button" onClick={logout} aria-label="Cikis yap" title="Cikis yap">
            <LogOut size={18} />
          </button>
        </header>

        <main className="content">
          {currentView === "dashboard" ? <DashboardView token={token} user={user} /> : null}
          {currentView === "forms" && user.role === "Admin" ? <FormDesignerDraft /> : null}
          {currentView === "runner" ? <FormRunnerDraft /> : null}
          {currentView === "processes" ? <ProcessBoardDraft mode="processes" role={user.role} /> : null}
          {currentView === "tasks" ? <ProcessBoardDraft mode="tasks" role={user.role} /> : null}
          {currentView === "settings" ? <SettingsView /> : null}
        </main>
      </div>
    </div>
  );
}

function isViewId(value: string | null): value is ViewId {
  return navItems.some((item) => item.viewId === value);
}

function DashboardView({ token, user }: { token: string | null; user: User }) {
  const [processes, setProcesses] = useState<ProcessSummary[]>([]);
  const [tasks, setTasks] = useState<ProcessTask[]>([]);
  const [status, setStatus] = useState<"loading" | "idle" | "error">("loading");

  useEffect(() => {
    let ignore = false;

    async function loadMetrics() {
      if (!token || token.startsWith("demo-")) {
        setStatus("idle");
        return;
      }

      try {
        setStatus("loading");
        const [processResult, taskResult] = await Promise.all([api.listProcesses(token), api.listMyTasks(token)]);
        if (!ignore) {
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

  return (
    <div className="view-panel">
      <section className="workspace-header">
        <div>
          <span className="eyebrow">Dashboard</span>
          <h1>Surec yonetimi paneli</h1>
        </div>
        <p>
          {status === "error"
            ? "Dashboard metrikleri yuklenemedi; API oturumunu kontrol et."
            : `${user.role} rolune gore surec ve is ozeti SQLite veritabanindan okunur.`}
        </p>
      </section>

      <section className="metric-grid" aria-label="Process summary">
        <article className="metric-card">
          <span>Bekleyen isler</span>
          <strong>{status === "loading" ? "-" : openTaskCount}</strong>
        </article>
        <article className="metric-card">
          <span>Devam eden surecler</span>
          <strong>{status === "loading" ? "-" : inProgressCount}</strong>
        </article>
        <article className="metric-card">
          <span>Tamamlanan surecler</span>
          <strong>{status === "loading" ? "-" : completedCount}</strong>
        </article>
      </section>

      <section className="flow-preview">
        <div className="flow-step">Oturum</div>
        <div className="flow-step">Form Definition</div>
        <div className="flow-step">Process Instance</div>
        <div className="flow-step">Task Action</div>
        <div className="flow-step">Audit Log</div>
      </section>
    </div>
  );
}

function SettingsView() {
  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Ayarlar</span>
          <h2>Uygulama ayarlari</h2>
        </div>
        <p>Bu alan sonraki adimda bildirim, tema ve surec varsayilanlari icin genisletilecek.</p>
      </div>
      <p className="empty-state">Simdilik tema degistirme ve oturum aksiyonlari ust bardan yonetiliyor.</p>
    </section>
  );
}
