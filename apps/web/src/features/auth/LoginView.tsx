"use client";

import { FormEvent, useState } from "react";
import { LogIn } from "lucide-react";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { api, ApiError } from "@/lib/api";
import { loginWithDemoUser } from "@/features/auth/demoUsers";
import { useSessionStore } from "@/features/session/sessionStore";

export function LoginView() {
  const setSession = useSessionStore((state) => state.setSession);
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("admin123");
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      const session = await api.login(username, password);
      setSession(session);
    } catch (apiError) {
      const demoSession = loginWithDemoUser(username, password);

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
          {error ? <div className="form-error">{error}</div> : null}
          <button className="primary-button" type="submit" disabled={isLoading}>
            <LogIn size={18} />
            {isLoading ? "Giris yapiliyor" : "Giris yap"}
          </button>
        </form>

        <div className="demo-users">
          <span>Demo:</span>
          <code>admin/admin123</code>
          <code>user/user123</code>
          <code>approver/approver123</code>
        </div>
      </section>
    </main>
  );
}
