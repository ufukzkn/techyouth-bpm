import { Sparkles, UserPlus } from "lucide-react";
import type { TranslationKey } from "@/features/i18n/translations";
import type { Community, CommunityRole, Role, User, UserStatus } from "@/lib/types";

type Translate = (key: TranslationKey, values?: Record<string, string | number>) => string;

export type CreateUserDraft = {
  username: string;
  displayName: string;
  email: string;
  role: Role;
  status: UserStatus;
  temporaryPassword: string;
  communityId: string;
  communityRoleId: string;
};

export function UserCreatePanel({
  activeUser,
  communities,
  communityRoles,
  draft,
  isCreating,
  isOpen,
  message,
  messageClassName,
  onDraftChange,
  onRequestCreate,
  onToggleCustomPassword,
  onToggleOpen,
  t,
  usesCustomTemporaryPassword,
}: {
  activeUser: User;
  communities: Community[];
  communityRoles: CommunityRole[];
  draft: CreateUserDraft;
  isCreating: boolean;
  isOpen: boolean;
  message: string | null;
  messageClassName: string;
  onDraftChange: (patch: Partial<CreateUserDraft>) => void;
  onRequestCreate: () => void;
  onToggleCustomPassword: (enabled: boolean) => void;
  onToggleOpen: () => void;
  t: Translate;
  usesCustomTemporaryPassword: boolean;
}) {
  return (
    <section className="identity-section user-create-disclosure user-create-left-panel">
      <div className="section-toolbar">
        <div>
          <span className="eyebrow">{t("users.createEyebrow")}</span>
          <h3>{t("users.createTitle")}</h3>
        </div>
        <button
          className={isOpen ? "secondary-button" : "success-button create-user-toggle-button"}
          type="button"
          onClick={onToggleOpen}
        >
          <UserPlus size={17} />
          {isOpen ? t("common.close") : t("users.createUser")}
        </button>
      </div>
      {isOpen ? (
        <div className="admin-create-panel">
          <div className="admin-create-grid">
            <input
              value={draft.username}
              onChange={(event) => onDraftChange({ username: event.target.value })}
              placeholder={t("login.username")}
            />
            <input
              value={draft.displayName}
              onChange={(event) => onDraftChange({ displayName: event.target.value })}
              placeholder={t("login.displayName")}
            />
            <input
              value={draft.email}
              onChange={(event) => onDraftChange({ email: event.target.value })}
              placeholder={t("login.email")}
              type="email"
            />
            {usesCustomTemporaryPassword ? (
              <input
                value={draft.temporaryPassword}
                onChange={(event) => onDraftChange({ temporaryPassword: event.target.value })}
                placeholder={t("users.temporaryPassword")}
                type="password"
              />
            ) : (
              <div className="generated-password-placeholder">
                <Sparkles size={16} />
                <span>{t("users.autoTemporaryPassword")}</span>
              </div>
            )}
            {activeUser.role === "SuperAdmin" ? (
              <select
                value={draft.communityId}
                onChange={(event) =>
                  onDraftChange({ communityId: event.target.value, communityRoleId: "" })
                }
              >
                <option value="">Topluluk seçin</option>
                {communities.map((community) => (
                  <option key={community.id} value={community.id}>
                    {community.name}
                  </option>
                ))}
              </select>
            ) : null}
            <select
              value={draft.communityRoleId}
              disabled={!draft.communityId}
              onChange={(event) => onDraftChange({ communityRoleId: event.target.value })}
            >
              <option value="">Topluluk rolü seçin</option>
              {communityRoles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </select>
          </div>
          {!draft.communityId ? (
            <p className="helper-copy">
              Kullanıcının bağlı olacağı topluluğu seçin; ardından topluluk rolünü atayın.
            </p>
          ) : null}
          <div className="admin-create-actions">
            <label className="checkbox-line custom-password-toggle compact-password-toggle">
              <input
                checked={usesCustomTemporaryPassword}
                onChange={(event) => onToggleCustomPassword(event.target.checked)}
                type="checkbox"
              />
              <span>{t("users.useCustomTemporaryPassword")}</span>
            </label>
            <button
              className="success-button create-user-submit-button"
              type="button"
              disabled={isCreating || !draft.communityId || !draft.communityRoleId}
              onClick={onRequestCreate}
            >
              {isCreating ? t("users.creatingUser") : t("users.createUser")}
            </button>
          </div>
          {message ? <div className={messageClassName}>{message}</div> : null}
        </div>
      ) : null}
    </section>
  );
}
