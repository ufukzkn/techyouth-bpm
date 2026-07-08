import { AlertTriangle, KeyRound, Save, ShieldCheck } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { AllSessionsRevokeDialog, OwnSessionRevokeDialog } from "@/features/app-shell/components/AccessDialogs";
import { DisclosureSection } from "@/features/app-shell/components/DisclosureSection";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { formatCountdown, formatIpAddress, formatSessionExpiry, summarizeUserAgent } from "@/features/app-shell/sessionFormatters";
import type { PendingSessionRevoke, SettingsSectionId, StatusTone } from "@/features/app-shell/types";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, User, UserSession } from "@/lib/types";

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
                <small>{t(session.rememberedDevice ? "settings.rememberedDevice" : "settings.standardSession")}</small>
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
