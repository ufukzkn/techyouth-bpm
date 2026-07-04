"use client";

import { FormEvent, useState } from "react";
import { LogIn, UserPlus } from "lucide-react";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { api, ApiError } from "@/lib/api";
import { demoUsers, loginWithDemoUser } from "@/features/auth/demoUsers";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";

export function LoginView() {
  const { clearSessionNotice, language, sessionNotice, setSession, theme, toggleLanguage, toggleTheme } =
    useSessionStore();
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [username, setUsername] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  function updateUsername(value: string) {
    setUsername(value);
    setSuccessMessage(null);
    clearSessionNotice();
  }

  function updatePassword(value: string) {
    setPassword(value);
    setSuccessMessage(null);
    clearSessionNotice();
  }

  function updateRememberMe(value: boolean) {
    setRememberMe(value);
    clearSessionNotice();
  }

  function switchMode(nextMode: "login" | "register") {
    setMode(nextMode);
    setError(null);
    setSuccessMessage(null);
    clearSessionNotice();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    clearSessionNotice();
    setError(null);
    setIsLoading(true);

    const formData = new FormData(event.currentTarget);
    const submittedUsername = String(formData.get("username") ?? "").trim();
    const submittedDisplayName = String(formData.get("displayName") ?? "").trim();
    const submittedEmail = String(formData.get("email") ?? "").trim();
    const submittedPassword = String(formData.get("password") ?? "");
    const submittedRememberMe = formData.get("rememberMe") === "on";

    if (mode === "register") {
      if (!submittedUsername || !submittedDisplayName || !submittedEmail || !submittedPassword) {
        setIsLoading(false);
        setError(t("login.registerRequired"));
        return;
      }

      try {
        const registration = await api.register(
          submittedUsername,
          submittedDisplayName,
          submittedEmail,
          submittedPassword,
        );
        setSuccessMessage(t("login.registerPending", { username: registration.username }));
        setMode("login");
        setPassword("");
      } catch (apiError) {
        setError(apiError instanceof ApiError ? apiError.errors.join(" ") : t("login.registerFailed"));
      } finally {
        setIsLoading(false);
      }

      return;
    }

    if (!submittedUsername || !submittedPassword) {
      setIsLoading(false);
      setError(t("login.required"));
      return;
    }

    try {
      const session = await api.login(submittedUsername, submittedPassword, submittedRememberMe);
      setSession(session);
    } catch (apiError) {
      const demoSession = loginWithDemoUser(submittedUsername, submittedPassword, submittedRememberMe);

      if (demoSession) {
        setSession(demoSession);
        return;
      }

      setError(apiError instanceof ApiError ? apiError.errors.join(" ") : t("login.invalid"));
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <main className="login-page">
      <div className="login-actions">
        <LanguageToggleButton language={language} label={t("common.language")} onToggle={toggleLanguage} />
        <ThemeToggleButton theme={theme} label={t("common.theme")} onToggle={toggleTheme} />
      </div>
      <section className="login-panel" aria-label="Login">
        <div className="login-mark">
          <PrototypeLogo size={34} />
        </div>
        <h1>TechYouth BPM Wizard</h1>
        <p>{language === "tr" ? "Form tasarimi ve surec yonetimi calisma alani" : "Form design and process management workspace"}</p>

        <div className="segmented-control" aria-label={t("login.authMode")}>
          <button
            className={mode === "login" ? "active" : undefined}
            type="button"
            onClick={() => switchMode("login")}
          >
            {t("login.signIn")}
          </button>
          <button
            className={mode === "register" ? "active" : undefined}
            type="button"
            onClick={() => switchMode("register")}
          >
            {t("login.createAccount")}
          </button>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            {t("login.username")}
            <input
              name="username"
              value={username}
              onChange={(event) => updateUsername(event.target.value)}
              autoComplete="username"
            />
          </label>
          {mode === "register" ? (
            <>
              <label>
                {t("login.displayName")}
                <input
                  name="displayName"
                  value={displayName}
                  onChange={(event) => {
                    setDisplayName(event.target.value);
                    setSuccessMessage(null);
                  }}
                  autoComplete="name"
                />
              </label>
              <label>
                {t("login.email")}
                <input
                  name="email"
                  value={email}
                  onChange={(event) => {
                    setEmail(event.target.value);
                    setSuccessMessage(null);
                  }}
                  autoComplete="email"
                  type="email"
                />
              </label>
            </>
          ) : null}
          <label>
            {t("login.password")}
            <input
              name="password"
              value={password}
              onChange={(event) => updatePassword(event.target.value)}
              type="password"
              autoComplete="current-password"
            />
          </label>
          {mode === "login" ? (
            <label className="checkbox-row remember-row">
              <input
                name="rememberMe"
                checked={rememberMe}
                onChange={(event) => updateRememberMe(event.target.checked)}
                type="checkbox"
              />
              {t("login.rememberMe")}
            </label>
          ) : null}
          {error ? <div className="form-error">{error}</div> : null}
          {successMessage ? <div className="form-success">{successMessage}</div> : null}
          <button className="primary-button" type="submit" disabled={isLoading}>
            {mode === "login" ? <LogIn size={18} /> : <UserPlus size={18} />}
            {isLoading
              ? mode === "login"
                ? t("login.signingIn")
                : t("login.registering")
              : mode === "login"
                ? t("login.signIn")
                : t("login.createAccount")}
          </button>
        </form>

        {sessionNotice ? (
          <div className="session-dialog-backdrop" role="presentation">
            <section className="session-dialog" role="alertdialog" aria-modal="true" aria-labelledby="session-dialog-title">
              <span className="eyebrow">{t("login.session")}</span>
              <h2 id="session-dialog-title">{t("login.noticeTitle")}</h2>
              <p>{sessionNotice}</p>
              <button className="primary-button" type="button" onClick={clearSessionNotice}>
                {t("login.noticeOk")}
              </button>
            </section>
          </div>
        ) : null}

        <div className="demo-users">
          <span>{t("login.demoUsers")}</span>
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
