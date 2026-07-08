"use client";

import { FormEvent, useEffect, useState } from "react";
import { KeyRound, LogIn, MailCheck, UserPlus } from "lucide-react";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { PrototypeLogo } from "@/features/app-shell/PrototypeLogo";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import { api } from "@/lib/api";
import { demoUsers, loginWithDemoUser } from "@/features/auth/demoUsers";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";

type InitialAuthState = {
  mode: "login" | "register" | "forgotPassword" | "verifyEmail";
  username: string;
  resetToken: string;
  successMessage: string | null;
};

function getInitialAuthState(language: "tr" | "en"): InitialAuthState {
  if (typeof window === "undefined") {
    return { mode: "login", username: "", resetToken: "", successMessage: null };
  }

  const params = new URLSearchParams(window.location.search);
  if (params.get("auth") !== "reset") {
    return { mode: "login", username: "", resetToken: "", successMessage: null };
  }

  return {
    mode: "forgotPassword",
    username: params.get("usernameOrEmail") ?? "",
    resetToken: params.get("token") ?? "",
    successMessage: translate(language, "login.resetLinkLoaded"),
  };
}

export function LoginView() {
  const { clearSessionNotice, language, sessionNotice, setSession, theme, toggleLanguage, toggleTheme } =
    useSessionStore();
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [initialAuthState] = useState(() => getInitialAuthState(language));
  const [username, setUsername] = useState(initialAuthState.username);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [resetToken, setResetToken] = useState(initialAuthState.resetToken);
  const [verificationCode, setVerificationCode] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [mode, setMode] = useState<"login" | "register" | "forgotPassword" | "verifyEmail">(initialAuthState.mode);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(initialAuthState.successMessage);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (window.location.search.includes("auth=reset")) {
      window.history.replaceState(null, "", window.location.pathname);
    }
  }, []);

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

  function switchMode(nextMode: "login" | "register" | "forgotPassword" | "verifyEmail") {
    setMode(nextMode);
    setError(null);
    setSuccessMessage(null);
    setResetToken("");
    setVerificationCode("");
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
    const submittedResetToken = String(formData.get("resetToken") ?? "").trim();
    const submittedVerificationCode = String(formData.get("verificationCode") ?? "").trim();
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
        setError(localizeApiError(apiError, language, t("login.registerFailed")));
      } finally {
        setIsLoading(false);
      }

      return;
    }

    if (mode === "forgotPassword") {
      if (!submittedUsername) {
        setIsLoading(false);
        setError(t("login.forgotRequired"));
        return;
      }

      try {
        if (!submittedResetToken) {
          const response = await api.forgotPassword({ usernameOrEmail: submittedUsername });
          setSuccessMessage(response.demoToken ? t("login.resetRequestedWithDemoToken", { token: response.demoToken }) : t("login.resetRequested"));
          if (response.demoToken) {
            setResetToken(response.demoToken);
          }
          return;
        }

        if (!submittedPassword) {
          setError(t("login.resetPasswordRequired"));
          return;
        }

        await api.resetPassword({
          usernameOrEmail: submittedUsername,
          token: submittedResetToken,
          newPassword: submittedPassword,
        });
        setSuccessMessage(t("login.resetCompleted"));
        setMode("login");
        setPassword("");
        setResetToken("");
      } catch (apiError) {
        setError(localizeApiError(apiError, language, t("login.resetFailed")));
      } finally {
        setIsLoading(false);
      }

      return;
    }

    if (mode === "verifyEmail") {
      if (!submittedUsername) {
        setIsLoading(false);
        setError(t("login.verificationRequired"));
        return;
      }

      try {
        if (!submittedVerificationCode) {
          await api.startPublicEmailVerification(submittedUsername);
          setSuccessMessage(t("login.verificationSent"));
        } else {
          await api.confirmPublicEmailVerification(submittedUsername, submittedVerificationCode);
          setSuccessMessage(t("login.verificationCompleted"));
          setMode("login");
          setVerificationCode("");
        }
      } catch (apiError) {
        setError(localizeApiError(apiError, language, t("login.verificationFailed")));
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

      setError(localizeApiError(apiError, language, t("login.invalid")));
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
            {mode === "forgotPassword" || mode === "verifyEmail" ? t("login.usernameOrEmail") : t("login.username")}
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
          {mode === "verifyEmail" ? (
            <label>
              {t("login.verificationCode")}
              <input
                name="verificationCode"
                value={verificationCode}
                onChange={(event) => {
                  setVerificationCode(event.target.value);
                  setSuccessMessage(null);
                }}
                inputMode="numeric"
              />
            </label>
          ) : null}
          {mode === "forgotPassword" ? (
            <label>
              {t("login.resetToken")}
              <input
                name="resetToken"
                value={resetToken}
                onChange={(event) => {
                  setResetToken(event.target.value);
                  setSuccessMessage(null);
                }}
              />
            </label>
          ) : null}
          {mode !== "verifyEmail" && (mode !== "forgotPassword" || resetToken || successMessage) ? (
            <label>
              {mode === "forgotPassword" ? t("login.newPassword") : t("login.password")}
              <input
                name="password"
                value={password}
                onChange={(event) => updatePassword(event.target.value)}
                type="password"
                autoComplete={mode === "forgotPassword" ? "new-password" : "current-password"}
              />
            </label>
          ) : null}
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
            {mode === "login" ? <LogIn size={18} /> : null}
            {mode === "register" ? <UserPlus size={18} /> : null}
            {mode === "forgotPassword" ? <KeyRound size={18} /> : null}
            {mode === "verifyEmail" ? <MailCheck size={18} /> : null}
            {isLoading ? t("common.loading") : null}
            {!isLoading && mode === "login" ? t("login.signIn") : null}
            {!isLoading && mode === "register" ? t("login.createAccount") : null}
            {!isLoading && mode === "forgotPassword"
              ? resetToken
                ? t("login.resetPassword")
                : t("login.sendResetToken")
              : null}
            {!isLoading && mode === "verifyEmail"
              ? verificationCode
                ? t("login.confirmVerification")
                : t("login.sendVerification")
              : null}
          </button>
        </form>

        <div className="login-secondary-actions">
          <button type="button" onClick={() => switchMode(mode === "forgotPassword" ? "login" : "forgotPassword")}>
            {mode === "forgotPassword" ? t("login.backToSignIn") : t("login.forgotPassword")}
          </button>
          <button type="button" onClick={() => switchMode(mode === "verifyEmail" ? "login" : "verifyEmail")}>
            {mode === "verifyEmail" ? t("login.backToSignIn") : t("login.verifyEmail")}
          </button>
        </div>

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
