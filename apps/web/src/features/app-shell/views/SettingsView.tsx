import { AlertTriangle } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { AllSessionsRevokeDialog, OwnSessionRevokeDialog } from "@/features/app-shell/components/AccessDialogs";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { formatCountdown } from "@/features/app-shell/sessionFormatters";
import type { PendingSessionRevoke, SettingsSectionId, StatusTone } from "@/features/app-shell/types";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, User, UserSession } from "@/lib/types";
import {
  EmailVerificationPanel,
  SettingsAccountActions,
  SettingsOverview,
  SettingsSessionsPanel,
} from "@/features/settings/SettingsSections";

const emailVerificationResendCooldownMs = 5 * 60 * 1000;

export function SettingsView({
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
  const [isPasswordChangeConfirmOpen, setIsPasswordChangeConfirmOpen] = useState(false);
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
      <SettingsOverview
        expiresAt={expiresAt}
        isApiSession={isApiSession}
        language={language}
        onToggleVerification={() => setIsEmailVerificationOpen((isOpen) => !isOpen)}
        t={t}
        user={user}
      />

      {!user.isEmailVerified && isEmailVerificationOpen ? (
        <EmailVerificationPanel
          code={verificationCode}
          cooldownLabel={verificationCooldownLabel}
          demoCode={demoCode}
          expiresAt={verificationExpiresAt}
          isApiSession={isApiSession}
          isCooldownActive={isVerificationCooldownActive}
          isRequesting={isRequestingVerification}
          language={language}
          onCodeChange={setVerificationCode}
          onConfirm={() => void confirmVerification()}
          onRequest={() => void requestVerification()}
          t={t}
        />
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

      <SettingsAccountActions
        currentPassword={currentPassword}
        isApiSession={isApiSession}
        isChangingPassword={isChangingPassword}
        isSavingProfile={isSavingProfile}
        newPassword={newPassword}
        onCurrentPasswordChange={setCurrentPassword}
        onNewPasswordChange={setNewPassword}
        onPasswordRequest={() => setIsPasswordChangeConfirmOpen(true)}
        onProfileDisplayNameChange={setProfileDisplayName}
        onProfileEmailChange={setProfileEmail}
        onProfileSave={() => void saveProfile()}
        onToggle={toggleSettingsSection}
        openSections={openSettingsSections}
        profileDisplayName={profileDisplayName}
        profileEmail={profileEmail}
        t={t}
        user={user}
      />

      <SettingsSessionsPanel
        currentPage={currentSessionPage}
        isApiSession={isApiSession}
        isLoading={isLoadingSettings}
        isOpen={openSettingsSections.sessions}
        language={language}
        onNextPage={() => setSessionPage((value) => Math.min(value + 1, sessionTotalPages))}
        onPageChange={setSessionPage}
        onPreviousPage={() => setSessionPage((value) => Math.max(value - 1, 1))}
        onRevokeAll={() => setIsAllSessionsRevokeOpen(true)}
        onRevokeSession={requestOwnSessionRevoke}
        onToggle={() => toggleSettingsSection("sessions")}
        sessions={sessions}
        t={t}
        totalPages={sessionTotalPages}
        visibleSessions={visibleSessions}
      />
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
      {isPasswordChangeConfirmOpen ? (
        <ConfirmationDialog
          eyebrow="Sifre degisikligi"
          title="Sifreniz degistirilsin mi?"
          description="Yeni sifre kaydedildikten sonra mevcut oturumunuz korunur; dilerseniz ayarlardan diger cihazlari ayrica kapatabilirsiniz."
          confirmLabel="Sifreyi degistir"
          tone="primary"
          onCancel={() => setIsPasswordChangeConfirmOpen(false)}
          onConfirm={() => {
            setIsPasswordChangeConfirmOpen(false);
            void changePassword();
          }}
        />
      ) : null}
    </section>
  );
}
