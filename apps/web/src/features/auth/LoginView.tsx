"use client";

import { FormEvent, useState } from "react";
import { LogIn, Moon, Sun } from "lucide-react";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { api, ApiError } from "@/lib/api";
import { demoUsers, loginWithDemoUser } from "@/features/auth/demoUsers";
import { useSessionStore } from "@/features/session/sessionStore";

export function LoginView() {
  const { clearSessionNotice, sessionNotice, setSession, theme, toggleTheme } = useSessionStore();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    clearSessionNotice();
    setError(null);
    setIsLoading(true);

    try {
      const session = await api.login(username, password, rememberMe);
      setSession(session);
    } catch (apiError) {
      const demoSession = loginWithDemoUser(username, password, rememberMe);

      if (demoSession) {
        setSession(demoSession);
        return;
      }

      setError(apiError instanceof ApiError ? apiError.errors.join(" ") : "Login failed.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <main className="login-page">
      <button className="login-theme-toggle icon-button" onClick={toggleTheme} aria-label="Tema degistir" title="Tema degistir">
        {theme === "light" ? <Moon size={18} /> : <Sun size={18} />}
      </button>
      <section className="login-panel" aria-label="Login">
        <div className="login-mark">
          <PrototypeLogo size={34} />
        </div>
        <h1>TechYouth BPM Wizard</h1>
        <p>Form tasarimi ve surec yonetimi calisma alani</p>

        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            Kullanici adi
            <input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" />
          </label>
          <label>
            Sifre
            <input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              autoComplete="current-password"
            />
          </label>
          <label className="checkbox-row remember-row">
            <input checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" />
            Beni hatirla
          </label>
          {error ? <div className="form-error">{error}</div> : null}
          <button className="primary-button" type="submit" disabled={isLoading}>
            <LogIn size={18} />
            {isLoading ? "Giris yapiliyor" : "Giris yap"}
          </button>
        </form>

        {sessionNotice ? (
          <div className="session-dialog-backdrop" role="presentation">
            <section className="session-dialog" role="alertdialog" aria-modal="true" aria-labelledby="session-dialog-title">
              <span className="eyebrow">Oturum</span>
              <h2 id="session-dialog-title">Oturum sona erdi</h2>
              <p>{sessionNotice}</p>
              <button className="primary-button" type="button" onClick={clearSessionNotice}>
                Tamam
              </button>
            </section>
          </div>
        ) : null}

        <div className="demo-users">
          <span>Demo hesaplar:</span>
          {demoUsers.map((demoUser) => (
            <button
              key={demoUser.username}
              type="button"
              onClick={() => {
                setUsername(demoUser.username);
                setPassword(demoUser.password);
                setError(null);
                clearSessionNotice();
              }}
            >
              {demoUser.username}
            </button>
          ))}
        </div>
      </section>
    </main>
  );
}
