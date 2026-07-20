import { Building2, Search, UserCog } from "lucide-react";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { userStatusLabel } from "@/features/app-shell/sessionFormatters";
import type { TranslationKey } from "@/features/i18n/translations";
import type {
  Community,
  CommunityRole,
  CommunitySummary,
  Language,
  User,
  UserStatus,
} from "@/lib/types";

type Translate = (key: TranslationKey, values?: Record<string, string | number>) => string;

export function UserManagementFilters({
  activeUser,
  allCommunitiesUserCount,
  communities,
  communityRoleFilter,
  communityRoles,
  isLoadingAllCommunitiesUserCount,
  isLoadingCommunities,
  isLoadingCommunitySummary,
  language,
  message,
  messageClassName,
  onCommunityChange,
  onRoleFilterChange,
  onSearchChange,
  onToggleStatus,
  searchQuery,
  selectedCommunity,
  selectedCommunityId,
  selectedCommunitySummary,
  selectedStatuses,
  t,
}: {
  activeUser: User;
  allCommunitiesUserCount: number | null;
  communities: Community[];
  communityRoleFilter: string | null;
  communityRoles: CommunityRole[];
  isLoadingAllCommunitiesUserCount: boolean;
  isLoadingCommunities: boolean;
  isLoadingCommunitySummary: boolean;
  language: Language;
  message: string | null;
  messageClassName: string;
  onCommunityChange: (communityId: string | null) => void;
  onRoleFilterChange: (communityRoleId: string | null) => void;
  onSearchChange: (query: string) => void;
  onToggleStatus: (status: UserStatus) => void;
  searchQuery: string;
  selectedCommunity: Community | null;
  selectedCommunityId: string | null;
  selectedCommunitySummary: CommunitySummary | null;
  selectedStatuses: UserStatus[];
  t: Translate;
}) {
  return (
    <div className="identity-section">
      <div className="filter-toolbar users-filter-toolbar">
        <label className="search-field">
          <Search size={16} />
          <input
            value={searchQuery}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder={t("users.searchPlaceholder")}
          />
        </label>
        <div className="user-community-scope">
          <Building2 aria-hidden="true" size={17} />
          <div>
            {activeUser.role === "SuperAdmin" ? (
              <>
                <select
                  value={selectedCommunityId ?? ""}
                  onChange={(event) => onCommunityChange(event.target.value || null)}
                >
                  <option value="">Tüm topluluklar</option>
                  {communities.map((community) => (
                    <option key={community.id} value={community.id}>
                      {community.name}
                    </option>
                  ))}
                </select>
                {!selectedCommunityId ? (
                  <small className="all-communities-summary">
                    {isLoadingCommunities
                    || isLoadingAllCommunitiesUserCount
                    || allCommunitiesUserCount === null ? (
                      <InlineValueLoader label="Toplam kullanıcı sayısı yükleniyor" />
                    ) : (
                      `${communities.length} topluluk · ${allCommunitiesUserCount} kullanıcı`
                    )}
                  </small>
                ) : null}
              </>
            ) : (
              <strong>{activeUser.communityName}</strong>
            )}
          </div>
          {selectedCommunityId ? (
            <span className="community-member-count">
              {isLoadingCommunitySummary || !selectedCommunitySummary ? (
                <InlineValueLoader label="Üye sayısı yükleniyor" />
              ) : (
                `${selectedCommunitySummary.memberCount} üye`
              )}
            </span>
          ) : null}
        </div>
        {selectedCommunityId ? (
          <label className="filter-select-field compact-filter-field">
            <UserCog size={16} />
            <select
              value={communityRoleFilter ?? ""}
              onChange={(event) => onRoleFilterChange(event.target.value || null)}
            >
              <option value="">Tüm roller</option>
              {communityRoles.map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>
      <div className="users-scope-summary">
        <div className="status-line">
          {selectedCommunity
            ? `${selectedCommunity.name} topluluğundaki kullanıcılar`
            : activeUser.role === "SuperAdmin"
              ? "Tüm topluluklardaki kullanıcılar"
              : activeUser.communityName}
        </div>
        <fieldset className="status-checkbox-filters">
          <legend>Durum</legend>
          {(["Active", "PendingApproval", "Rejected"] as UserStatus[]).map((status) => (
            <label key={status}>
              <input
                checked={selectedStatuses.includes(status)}
                onChange={() => onToggleStatus(status)}
                type="checkbox"
              />
              <span>{userStatusLabel(language, status)}</span>
            </label>
          ))}
        </fieldset>
      </div>
      {message ? <div className={messageClassName}>{message}</div> : null}
    </div>
  );
}
