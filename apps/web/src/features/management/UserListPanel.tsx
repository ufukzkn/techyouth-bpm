import { UserCog } from "lucide-react";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { userStatusLabel } from "@/features/app-shell/sessionFormatters";
import type { TranslationKey } from "@/features/i18n/translations";
import type { Language, UserAdmin } from "@/lib/types";

type Translate = (key: TranslationKey, values?: Record<string, string | number>) => string;

export function UserListPanel({
  currentPage,
  isLoading,
  language,
  onNextPage,
  onPageChange,
  onPreviousPage,
  onSelect,
  selectedUserId,
  t,
  totalPages,
  users,
}: {
  currentPage: number;
  isLoading: boolean;
  language: Language;
  onNextPage: () => void;
  onPageChange: (page: number) => void;
  onPreviousPage: () => void;
  onSelect: (user: UserAdmin) => void;
  selectedUserId: string | null;
  t: Translate;
  totalPages: number;
  users: UserAdmin[];
}) {
  return (
    <section className="identity-section">
      <div className="section-toolbar">
        <div><span className="eyebrow">{t("users.listEyebrow")}</span><h3>{t("users.listTitle")}</h3></div>
        <UserCog size={22} />
      </div>
      <div className="user-management-list">
        {isLoading ? <UserManagementSkeleton /> : null}
        {!isLoading ? users.map((user) => (
          <article className="settings-row user-management-row" key={user.id}>
            <div className="stacked-summary">
              <span className={`status-pill status-${user.status.toLowerCase()}`}>{userStatusLabel(language, user.status)}</span>
              <strong>{user.displayName}</strong>
              <small>{user.username} / {user.email}</small>
            </div>
            <button
              className={`secondary-button context-button ${selectedUserId === user.id ? "is-active" : ""}`}
              onClick={() => onSelect(user)}
              type="button"
            >
              {t("users.viewDetail")}
            </button>
          </article>
        )) : null}
        {!users.length && !isLoading ? <p className="status-line">{t("users.empty")}</p> : null}
      </div>
      <PaginationControls
        currentPage={currentPage}
        language={language}
        onNext={onNextPage}
        onPageChange={onPageChange}
        onPrevious={onPreviousPage}
        totalPages={totalPages}
      />
    </section>
  );
}

function UserManagementSkeleton() {
  return (
    <>
      {Array.from({ length: 4 }, (_, index) => (
        <article className="settings-row user-management-row user-management-skeleton" key={index}>
          <div className="stacked-summary"><span /><strong /><small /></div>
          <span />
        </article>
      ))}
    </>
  );
}
