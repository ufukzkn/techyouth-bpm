import { AlertTriangle, LogOut } from "lucide-react";
import { useCallback, useState } from "react";
import { LanguageToggleButton } from "@/features/app-shell/LanguageToggleButton";
import { ThemeToggleButton } from "@/features/app-shell/ThemeToggleButton";
import type { StatusTone } from "@/features/app-shell/types";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, User } from "@/lib/types";

export function ForcedPasswordChangeView({
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
