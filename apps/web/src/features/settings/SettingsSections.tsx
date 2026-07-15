import { KeyRound, Save, ShieldCheck } from "lucide-react";
import { DisclosureSection } from "@/features/app-shell/components/DisclosureSection";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { formatIpAddress, formatSessionExpiry, summarizeUserAgent } from "@/features/app-shell/sessionFormatters";
import type { SettingsSectionId } from "@/features/app-shell/types";
import type { TranslationKey } from "@/features/i18n/translations";
import type { Language, User, UserSession } from "@/lib/types";

type Translate = (key: TranslationKey, values?: Record<string, string | number>) => string;

export function SettingsOverview({
  expiresAt,
  isApiSession,
  language,
  onToggleVerification,
  t,
  user,
}: {
  expiresAt: string | null;
  isApiSession: boolean;
  language: Language;
  onToggleVerification: () => void;
  t: Translate;
  user: User;
}) {
  return (
    <div className="settings-grid">
      <article className="settings-row">
        <span>{t("settings.profile")}</span>
        <strong>{user.displayName}</strong>
        <small>{user.email || t("settings.noEmail")}</small>
      </article>
      <article className="settings-row">
        <span>{t("settings.emailStatus")}</span>
        <strong className={user.isEmailVerified ? "verified-status" : undefined}>
          {user.isEmailVerified ? <><ShieldCheck size={18} />{t("settings.verified")}</> : t("settings.notVerified")}
        </strong>
        {!user.isEmailVerified ? (
          <div className="settings-card-action">
            <button className="text-link-button" disabled={!isApiSession} onClick={onToggleVerification} type="button">
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
  );
}

export function EmailVerificationPanel({
  code,
  cooldownLabel,
  demoCode,
  expiresAt,
  isApiSession,
  isCooldownActive,
  isRequesting,
  language,
  onCodeChange,
  onConfirm,
  onRequest,
  t,
}: {
  code: string;
  cooldownLabel: string;
  demoCode: string | null;
  expiresAt: string | null;
  isApiSession: boolean;
  isCooldownActive: boolean;
  isRequesting: boolean;
  language: Language;
  onCodeChange: (value: string) => void;
  onConfirm: () => void;
  onRequest: () => void;
  t: Translate;
}) {
  return (
    <section className="identity-section email-verification-section">
      <div className="section-toolbar">
        <div><span className="eyebrow">{t("settings.emailVerificationTitle")}</span><h3>{t("settings.verifyEmail")}</h3></div>
        <ShieldCheck size={22} />
      </div>
      <div className="inline-verification-form">
        <button className="secondary-button" disabled={!isApiSession || isRequesting || isCooldownActive} onClick={onRequest} type="button">
          {isRequesting ? <span aria-hidden="true" className="button-spinner" /> : null}
          {isCooldownActive
            ? t("settings.resendAvailableIn", { value: cooldownLabel })
            : expiresAt ? t("settings.resendVerificationCode") : t("settings.sendVerificationCode")}
        </button>
        <small>{expiresAt ? t("settings.verificationValidUntil", { value: formatSessionExpiry(expiresAt, language) }) : t("settings.verificationValidityHint")}</small>
        {expiresAt ? <small>{isCooldownActive ? t("settings.resendCooldownHint", { value: cooldownLabel }) : t("settings.resendReadyHint")}</small> : null}
        <input onChange={(event) => onCodeChange(event.target.value)} placeholder={t("settings.verificationCode")} value={code} />
        <button className="primary-button" disabled={!isApiSession} onClick={onConfirm} type="button">{t("settings.verifyEmail")}</button>
        {demoCode ? <small>{t("settings.demoVerificationCode", { code: demoCode })}</small> : null}
      </div>
    </section>
  );
}

export function SettingsAccountActions({
  currentPassword,
  isApiSession,
  isChangingPassword,
  isSavingProfile,
  newPassword,
  onCurrentPasswordChange,
  onNewPasswordChange,
  onPasswordRequest,
  onProfileDisplayNameChange,
  onProfileEmailChange,
  onProfileSave,
  onToggle,
  openSections,
  profileDisplayName,
  profileEmail,
  t,
  user,
}: {
  currentPassword: string;
  isApiSession: boolean;
  isChangingPassword: boolean;
  isSavingProfile: boolean;
  newPassword: string;
  onCurrentPasswordChange: (value: string) => void;
  onNewPasswordChange: (value: string) => void;
  onPasswordRequest: () => void;
  onProfileDisplayNameChange: (value: string) => void;
  onProfileEmailChange: (value: string) => void;
  onProfileSave: () => void;
  onToggle: (section: SettingsSectionId) => void;
  openSections: Record<SettingsSectionId, boolean>;
  profileDisplayName: string;
  profileEmail: string;
  t: Translate;
  user: User;
}) {
  return (
    <div className="settings-action-grid">
      <DisclosureSection
        description={t("settings.profileDescription")}
        eyebrow={t("settings.profile")}
        icon={<Save size={20} />}
        isOpen={openSections.profile}
        onToggle={() => onToggle("profile")}
        title={t("settings.profileTitle")}
      >
        <div className="compact-form">
          <input onChange={(event) => onProfileDisplayNameChange(event.target.value)} placeholder={t("login.displayName")} value={profileDisplayName} />
          <input onChange={(event) => onProfileEmailChange(event.target.value)} placeholder={t("login.email")} type="email" value={profileEmail} />
          <button className="primary-button" disabled={!isApiSession || isSavingProfile} onClick={onProfileSave} type="button">
            {isSavingProfile ? t("common.saving") : t("settings.saveProfile")}
          </button>
        </div>
      </DisclosureSection>
      <DisclosureSection
        className={user.mustChangePassword ? "urgent-identity-section" : undefined}
        description={t("settings.passwordDescription")}
        eyebrow={t("settings.auth")}
        icon={<KeyRound size={20} />}
        isOpen={openSections.password}
        onToggle={() => onToggle("password")}
        title={t("settings.passwordTitle")}
      >
        <div className="compact-form">
          <input onChange={(event) => onCurrentPasswordChange(event.target.value)} placeholder={t("settings.currentPassword")} type="password" value={currentPassword} />
          <input onChange={(event) => onNewPasswordChange(event.target.value)} placeholder={t("settings.newPassword")} type="password" value={newPassword} />
          <button
            className={user.mustChangePassword ? "primary-button danger-button" : "primary-button"}
            disabled={!isApiSession || isChangingPassword || !currentPassword || !newPassword}
            onClick={onPasswordRequest}
            type="button"
          >
            {isChangingPassword ? t("common.saving") : t("settings.changePassword")}
          </button>
        </div>
      </DisclosureSection>
    </div>
  );
}

export function SettingsSessionsPanel({
  currentPage,
  isApiSession,
  isLoading,
  isOpen,
  language,
  onNextPage,
  onPageChange,
  onPreviousPage,
  onRevokeAll,
  onRevokeSession,
  onToggle,
  sessions,
  t,
  totalPages,
  visibleSessions,
}: {
  currentPage: number;
  isApiSession: boolean;
  isLoading: boolean;
  isOpen: boolean;
  language: Language;
  onNextPage: () => void;
  onPageChange: (page: number) => void;
  onPreviousPage: () => void;
  onRevokeAll: () => void;
  onRevokeSession: (session: UserSession) => void;
  onToggle: () => void;
  sessions: UserSession[];
  t: Translate;
  totalPages: number;
  visibleSessions: UserSession[];
}) {
  return (
    <div className="settings-disclosure-stack">
      <DisclosureSection
        description={t("settings.sessionsDescription")}
        eyebrow={t("settings.sessions")}
        icon={<ShieldCheck size={22} />}
        isOpen={isOpen}
        onToggle={onToggle}
        title={t("settings.sessionsTitle")}
      >
        {isLoading ? <p className="status-line">{t("common.loading")}</p> : null}
        <div className="session-list">
          {visibleSessions.map((session) => (
            <article className="settings-row session-row" key={session.id}>
              <div className="stacked-summary">
                <span>{session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}</span>
                <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                <small>{session.lastSeenAt ? t("settings.lastSeen", { value: formatSessionExpiry(session.lastSeenAt, language) }) : t("settings.notSeenYet")}</small>
                <small>{t("settings.createdAt", { value: formatSessionExpiry(session.createdAt, language) })}</small>
                <small>{t("settings.device", { value: summarizeUserAgent(session.userAgent, language) })}</small>
                <small>{t("settings.ipAddress", { value: formatIpAddress(session.ipAddress, language) })}</small>
                <small>{t(session.rememberedDevice ? "settings.rememberedDevice" : "settings.standardSession")}</small>
              </div>
              <button className="secondary-button danger-button" disabled={!isApiSession} onClick={() => onRevokeSession(session)} type="button">
                {t("settings.revokeSession")}
              </button>
            </article>
          ))}
          {!sessions.length && !isLoading ? <p className="status-line">{t("settings.noSessions")}</p> : null}
        </div>
        {sessions.length > 4 ? (
          <PaginationControls
            currentPage={currentPage}
            language={language}
            onNext={onNextPage}
            onPageChange={onPageChange}
            onPrevious={onPreviousPage}
            totalPages={totalPages}
          />
        ) : null}
        <div className="session-danger-action">
          <button className="danger-button strong-danger-button" disabled={!isApiSession} onClick={onRevokeAll} type="button">
            {t("settings.revokeAllSessions")}
          </button>
        </div>
      </DisclosureSection>
    </div>
  );
}
