import { AlertTriangle } from "lucide-react";
import { useCallback } from "react";
import { formatSessionExpiry, userStatusLabel } from "@/features/app-shell/sessionFormatters";
import type { PendingAccessChange, PendingSessionRevoke, PendingUserDelete } from "@/features/app-shell/types";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export function OwnSessionRevokeDialog({
  revoke,
  language,
  onCancel,
  onConfirm,
}: {
  revoke: PendingSessionRevoke;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("settings.sessionRevokeEyebrow")}</span>
            <strong>{t(revoke.isCurrent ? "settings.currentSessionRevokeTitle" : "settings.sessionRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t(revoke.isCurrent ? "settings.currentSessionRevokeDescription" : "settings.sessionRevokeDescription", {
            expiresAt: formatSessionExpiry(revoke.expiresAt, language),
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("settings.revokeSession")}
          </button>
        </div>
      </div>
    </div>
  );
}

export function AllSessionsRevokeDialog({
  sessionCount,
  language,
  onCancel,
  onConfirm,
}: {
  sessionCount: number;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("settings.allSessionsRevokeEyebrow")}</span>
            <strong>{t("settings.allSessionsRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("settings.allSessionsRevokeDescription", {
            count: sessionCount,
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("settings.revokeAllSessions")}
          </button>
        </div>
      </div>
    </div>
  );
}

export function AccessChangeDialog({
  change,
  language,
  onCancel,
  onConfirm,
}: {
  change: PendingAccessChange;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const isHighRisk = change.toStatus !== change.fromStatus || change.fromCommunityRoleName !== change.toCommunityRoleName;
  const isDestructive = change.toStatus !== "Active" && change.toStatus !== change.fromStatus;

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.accessConfirmEyebrow")}</span>
            <strong>{t(isHighRisk ? "users.accessConfirmTitleCritical" : "users.accessConfirmTitle")}</strong>
          </div>
          <AlertTriangle className={isDestructive ? undefined : "primary-dialog-icon"} size={22} />
        </div>
        <p className="helper-copy">
          {t("users.accessConfirmDescription", {
            displayName: change.displayName,
            username: change.username,
          })}
        </p>
        <div className="access-confirm-grid">
          <article className="settings-row">
            <span>Topluluk rolü</span>
            <strong>
              {change.fromCommunityRoleName || "Atanmadı"} -&gt; {change.toCommunityRoleName || "Atanmadı"}
            </strong>
          </article>
          <article className="settings-row">
            <span>{t("settings.status")}</span>
            <strong>
              {userStatusLabel(language, change.fromStatus)} -&gt; {userStatusLabel(language, change.toStatus)}
            </strong>
          </article>
        </div>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className={isDestructive ? "danger-button strong-danger-button" : "primary-button"} type="button" onClick={onConfirm}>
            {t("users.confirmAccessChange")}
          </button>
        </div>
      </div>
    </div>
  );
}

export function SessionRevokeDialog({
  revoke,
  language,
  onCancel,
  onConfirm,
}: {
  revoke: PendingSessionRevoke;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.sessionRevokeEyebrow")}</span>
            <strong>{t("users.sessionRevokeTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("users.sessionRevokeDescription", {
            displayName: revoke.displayName,
            username: revoke.username,
            expiresAt: formatSessionExpiry(revoke.expiresAt, language),
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("users.confirmSessionRevoke")}
          </button>
        </div>
      </div>
    </div>
  );
}

export function UserDeleteDialog({
  deletion,
  language,
  onCancel,
  onConfirm,
}: {
  deletion: PendingUserDelete;
  language: Language;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className="action-dialog access-confirm-dialog" onClick={(event) => event.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">{t("users.deleteConfirmEyebrow")}</span>
            <strong>{t("users.deleteConfirmTitle")}</strong>
          </div>
          <AlertTriangle size={22} />
        </div>
        <p className="helper-copy">
          {t("users.deleteConfirmDescription", {
            displayName: deletion.displayName,
            username: deletion.username,
          })}
        </p>
        <div className="action-dialog-actions">
          <button className="secondary-button" type="button" onClick={onCancel}>
            {t("common.cancel")}
          </button>
          <button className="danger-button strong-danger-button" type="button" onClick={onConfirm}>
            {t("users.confirmDeleteUser")}
          </button>
        </div>
      </div>
    </div>
  );
}
