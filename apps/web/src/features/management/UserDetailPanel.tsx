import { ChevronDown, History, Info, ShieldCheck, X } from "lucide-react";
import { ActionFeedback } from "@/features/app-shell/components/AsyncState";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { SystemAuditTimeline } from "@/features/app-shell/components/SystemAuditTimeline";
import {
  formatIpAddress,
  formatSessionExpiry,
  summarizeUserAgent,
} from "@/features/app-shell/sessionFormatters";
import type { AccessDraft } from "@/features/app-shell/types";
import type { TranslationKey } from "@/features/i18n/translations";
import { UserTeamMembershipPanel } from "@/features/teams/UserTeamMembershipPanel";
import type {
  CommunityRole,
  Language,
  SystemAuditLog,
  User,
  UserAdmin,
  UserSession,
  UserStatus,
} from "@/lib/types";

type Translate = (key: TranslationKey, values?: Record<string, string | number>) => string;
type PasswordResetFeedback = {
  tone: "success" | "error" | "loading";
  text: string;
} | null;

export function UserDetailPanel({
  accessDraft,
  activeSessionCount,
  activeUser,
  currentSessionPage,
  detailCommunityRoles,
  hasDraftChanges,
  isLoadingDetailCommunityRoles,
  isLoadingUserLogs,
  isLoadingUserSessions,
  isPasswordResetting,
  isUserLogHistoryOpen,
  language,
  onAccessDraftChange,
  onClose,
  onDelete,
  onHistoryToggle,
  onNextSessionPage,
  onPasswordReset,
  onPreviousSessionPage,
  onRefreshUsers,
  onRequestAccessChange,
  onSessionPageChange,
  onSessionRevoke,
  passwordResetFeedback,
  selectedUser,
  selectedUserLogs,
  selectedUserSessions,
  sessionTotalPages,
  t,
  token,
  visibleSessions,
}: {
  accessDraft: AccessDraft | null;
  activeSessionCount: number;
  activeUser: User;
  currentSessionPage: number;
  detailCommunityRoles: CommunityRole[];
  hasDraftChanges: boolean;
  isLoadingDetailCommunityRoles: boolean;
  isLoadingUserLogs: boolean;
  isLoadingUserSessions: boolean;
  isPasswordResetting: boolean;
  isUserLogHistoryOpen: boolean;
  language: Language;
  onAccessDraftChange: (draft: AccessDraft) => void;
  onClose: () => void;
  onDelete: (user: UserAdmin) => void;
  onHistoryToggle: () => void;
  onNextSessionPage: () => void;
  onPasswordReset: () => void;
  onPreviousSessionPage: () => void;
  onRefreshUsers: () => void;
  onRequestAccessChange: () => void;
  onSessionPageChange: (page: number) => void;
  onSessionRevoke: (session: UserSession) => void;
  passwordResetFeedback: PasswordResetFeedback;
  selectedUser: UserAdmin | null;
  selectedUserLogs: SystemAuditLog[];
  selectedUserSessions: UserSession[];
  sessionTotalPages: number;
  t: Translate;
  token: string | null;
  visibleSessions: UserSession[];
}) {
  return (
    <section
      className={
        selectedUser
          ? "identity-section user-detail-panel detail-expanded"
          : "identity-section user-detail-panel detail-placeholder"
      }
    >
      <div className="section-toolbar">
        <div>
          <span className="eyebrow">{t("users.detailEyebrow")}</span>
          <h3>{selectedUser ? selectedUser.displayName : t("users.noSelection")}</h3>
        </div>
        {selectedUser ? (
          <button className="icon-button" type="button" onClick={onClose} title={t("common.close")}>
            <X size={18} />
          </button>
        ) : (
          <Info size={22} />
        )}
      </div>
      {selectedUser ? (
        <div className="user-detail-content">
          <div className="settings-grid compact-grid">
            <article className="settings-row">
              <span>{t("session.username")}</span>
              <strong>{selectedUser.username}</strong>
            </article>
            <article className="settings-row">
              <span>Topluluk rolü</span>
              <strong>{selectedUser.communityRoleName || "Atanmadı"}</strong>
              <small>{selectedUser.communityName || "Topluluk atanmadı"}</small>
            </article>
            <article className="settings-row">
              <span>Topluluk</span>
              <strong>{selectedUser.communityName || "-"}</strong>
            </article>
            <article className="settings-row">
              <span>{t("settings.emailStatus")}</span>
              <strong>
                {selectedUser.isEmailVerified ? t("settings.verified") : t("settings.notVerified")}
              </strong>
            </article>
            <article className="settings-row">
              <span>{t("users.onlineStatus")}</span>
              <strong>{activeSessionCount > 0 ? t("users.online") : t("users.offline")}</strong>
              <small>{t("users.activeSessionCount", { count: activeSessionCount })}</small>
            </article>
            <article className="settings-row">
              <span>{t("users.mustChangePassword")}</span>
              <strong>{selectedUser.mustChangePassword ? t("common.yes") : t("common.no")}</strong>
            </article>
          </div>

          <div className="access-editor">
            <select
              value={accessDraft?.communityRoleId ?? selectedUser.communityRoleId ?? ""}
              onChange={(event) =>
                onAccessDraftChange({
                  userId: selectedUser.id,
                  status: accessDraft?.status ?? selectedUser.status,
                  communityId: selectedUser.communityId,
                  communityRoleId: event.target.value,
                })
              }
            >
              {detailCommunityRoles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </select>
            <select
              value={accessDraft?.status ?? selectedUser.status}
              onChange={(event) =>
                onAccessDraftChange({
                  userId: selectedUser.id,
                  status: event.target.value as UserStatus,
                  communityId: selectedUser.communityId,
                  communityRoleId: accessDraft?.communityRoleId ?? selectedUser.communityRoleId,
                })
              }
            >
              <option value="PendingApproval">{t("settings.statusPending")}</option>
              <option value="Active">{t("settings.statusActive")}</option>
              <option value="Rejected">{t("settings.statusRejected")}</option>
            </select>
            <button
              className="primary-button"
              type="button"
              disabled={!hasDraftChanges || isLoadingDetailCommunityRoles}
              onClick={onRequestAccessChange}
            >
              {t("users.applyAccessChange")}
            </button>
          </div>

          {activeUser.role === "SuperAdmin" && selectedUser.role !== "SuperAdmin" ? (
            <div className="password-reset-inline">
              <button
                className="danger-button"
                disabled={isPasswordResetting}
                onClick={onPasswordReset}
                type="button"
              >
                {isPasswordResetting ? "Gönderiliyor" : "Şifreyi sıfırla"}
              </button>
              <ActionFeedback feedback={passwordResetFeedback} />
            </div>
          ) : null}

          {selectedUser.role !== "SuperAdmin" ? (
            <UserTeamMembershipPanel
              activeUser={activeUser}
              key={selectedUser.id}
              language={language}
              onChanged={onRefreshUsers}
              selectedUser={selectedUser}
              token={token}
            />
          ) : null}

          <section className="identity-section nested-identity-section">
            <div className="section-toolbar">
              <div>
                <span className="eyebrow">{t("users.sessionsEyebrow")}</span>
                <h3>{t("users.sessionsTitle")}</h3>
              </div>
              <ShieldCheck size={22} />
            </div>
            {isLoadingUserSessions ? <p className="status-line">{t("common.loading")}</p> : null}
            <div className="session-list">
              {visibleSessions.map((session) => (
                <article className="settings-row session-row" key={session.id}>
                  <div className="stacked-summary">
                    <span>
                      {session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}
                    </span>
                    <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                    <small>
                      {session.lastSeenAt
                        ? t("settings.lastSeen", {
                            value: formatSessionExpiry(session.lastSeenAt, language),
                          })
                        : t("settings.notSeenYet")}
                    </small>
                    <small>
                      {t("settings.createdAt", {
                        value: formatSessionExpiry(session.createdAt, language),
                      })}
                    </small>
                    <small>
                      {t("settings.device", {
                        value: summarizeUserAgent(session.userAgent, language),
                      })}
                    </small>
                    <small>
                      {t("settings.ipAddress", {
                        value: formatIpAddress(session.ipAddress, language),
                      })}
                    </small>
                    <small>
                      {t(
                        session.rememberedDevice
                          ? "settings.rememberedDevice"
                          : "settings.standardSession",
                      )}
                    </small>
                  </div>
                  <button
                    className="secondary-button danger-button"
                    type="button"
                    disabled={session.isCurrent && selectedUser.id === activeUser.id}
                    onClick={() => onSessionRevoke(session)}
                  >
                    {t("settings.revokeSession")}
                  </button>
                </article>
              ))}
              {!selectedUserSessions.length && !isLoadingUserSessions ? (
                <p className="status-line">{t("users.noActiveSessions")}</p>
              ) : null}
            </div>
            {selectedUserSessions.length > 4 ? (
              <PaginationControls
                currentPage={currentSessionPage}
                language={language}
                onNext={onNextSessionPage}
                onPageChange={onSessionPageChange}
                onPrevious={onPreviousSessionPage}
                totalPages={sessionTotalPages}
              />
            ) : null}
          </section>

          <div className="user-log-disclosure">
            <button className="text-button" type="button" onClick={onHistoryToggle}>
              <History size={16} />
              Kronolojik gecmis
              <ChevronDown
                className={isUserLogHistoryOpen ? "nav-group-chevron open" : "nav-group-chevron"}
                size={16}
              />
            </button>
            {isUserLogHistoryOpen ? (
              <SystemAuditTimeline
                logs={selectedUserLogs}
                language={language}
                emptyText={t("users.noUserLogs")}
                isLoading={isLoadingUserLogs}
              />
            ) : null}
          </div>
          <div className="detail-danger-action">
            <button
              className="danger-button strong-danger-button"
              type="button"
              disabled={selectedUser.id === activeUser.id}
              onClick={() => onDelete(selectedUser)}
            >
              {t("users.deleteUser")}
            </button>
          </div>
        </div>
      ) : (
        <p className="status-line">{t("users.noSelectionHelp")}</p>
      )}
    </section>
  );
}
