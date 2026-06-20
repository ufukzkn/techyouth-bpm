"use client";

import { LogOut, Moon, Sun } from "lucide-react";
import { useEffect } from "react";
import { navItems } from "@/features/app-shell/navigation";
import { LoginView } from "@/features/auth/LoginView";
import { useSessionStore } from "@/features/session/sessionStore";

export function AppShell() {
  const { user, theme, logout, toggleTheme } = useSessionStore();

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  if (!user) {
    return <LoginView />;
  }

  const visibleNavItems = navItems.filter((item) => item.roles.includes(user.role));

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-symbol">T</span>
          <div>
            <strong>TechYouth BPM</strong>
            <span>Wizard workspace</span>
          </div>
        </div>
        <nav className="side-nav" aria-label="Main navigation">
          {visibleNavItems.map((item) => {
            const Icon = item.icon;
            return (
              <a key={item.href} href={item.href}>
                <Icon size={18} />
                {item.label}
              </a>
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
          <section className="workspace-header" id="dashboard">
            <div>
              <span className="eyebrow">Dashboard</span>
              <h1>Surec yonetimi paneli</h1>
            </div>
            <p>Form tasarimi, baslatilan surecler ve bekleyen isler tek uygulama akisi icinde izlenecek.</p>
          </section>

          <section className="metric-grid" aria-label="Process summary">
            <article className="metric-card">
              <span>Bekleyen isler</span>
              <strong>3</strong>
            </article>
            <article className="metric-card">
              <span>Devam eden surecler</span>
              <strong>5</strong>
            </article>
            <article className="metric-card">
              <span>Tamamlanan surecler</span>
              <strong>12</strong>
            </article>
          </section>

          <section className="flow-preview">
            <div className="flow-step">Login</div>
            <div className="flow-step">Form Tasarla</div>
            <div className="flow-step">Surec Baslat</div>
            <div className="flow-step">Onayla / Reddet</div>
            <div className="flow-step">Detay Goruntule</div>
          </section>
        </main>
      </div>
    </div>
  );
}
