import { Crown, Network, UserPlus, UsersRound } from "lucide-react";
import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { EmptyState } from "@/features/ui/EmptyState";
import type { Language, TeamCandidate, TeamCandidatePage, TeamMember, TeamMemberPage, TeamPage } from "@/lib/types";

export function TeamListSkeleton() {
  return (
    <div aria-label="Takimlar yukleniyor" className="team-list team-list-skeleton" role="status">
      {Array.from({ length: 4 }, (_, index) => (
        <div className="team-list-card" key={index}>
          <SkeletonBlock className="team-skeleton-title" />
          <SkeletonBlock className="team-skeleton-copy" />
          <SkeletonBlock className="team-skeleton-meta" />
        </div>
      ))}
    </div>
  );
}

export function TeamListPanel({
  language,
  isLoading,
  onPageChange,
  onSelectTeam,
  onSelectUnassigned,
  result,
  selectedCommunityId,
  selectedTeamId,
  showUnassigned,
}: {
  language: Language;
  isLoading: boolean;
  onPageChange: (page: number) => void;
  onSelectTeam: (teamId: string) => void;
  onSelectUnassigned: () => void;
  result: TeamPage | null;
  selectedCommunityId: string | null;
  selectedTeamId: string | "unassigned" | null;
  showUnassigned: boolean;
}) {
  const isTr = language === "tr";
  if (isLoading && !result) return <TeamListSkeleton />;

  const teams = result?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / Math.max(1, result?.pageSize ?? 6)));

  return (
    <>
      <div className="team-list">
        {showUnassigned && selectedCommunityId ? (
          <button
            className={selectedTeamId === "unassigned" ? "team-list-card is-selected is-virtual" : "team-list-card is-virtual"}
            onClick={onSelectUnassigned}
            type="button"
          >
            <span className="team-card-icon"><UsersRound size={18} /></span>
            <span className="team-card-copy">
              <strong>{isTr ? "Takimsiz" : "Unassigned"}</strong>
              <small>{isTr ? "Aktif bir takim uyeligi bulunmayan kullanicilar" : "Users without an active team membership"}</small>
            </span>
            <span className="team-card-count">{result?.unassignedCount ?? 0}</span>
          </button>
        ) : null}

        {teams.map((team) => (
          <button
            className={selectedTeamId === team.id ? "team-list-card is-selected" : "team-list-card"}
            key={team.id}
            onClick={() => onSelectTeam(team.id)}
            type="button"
          >
            <span className="team-card-icon"><Network size={18} /></span>
            <span className="team-card-copy">
              <span className="team-card-title"><strong>{team.name}</strong><i className={team.isActive ? "status-dot is-active" : "status-dot"} /></span>
              <small>{team.communityName}</small>
              <small>{team.description || (isTr ? "Aciklama eklenmemis" : "No description")}</small>
            </span>
            <span className="team-card-metrics"><span><UsersRound size={14} /> {team.memberCount}</span><span><Crown size={14} /> {team.leadCount}</span></span>
          </button>
        ))}

        {!teams.length && !(showUnassigned && selectedCommunityId) ? (
          <EmptyState
            description={isTr ? "Arama veya kapsam secimini degistirerek yeniden deneyin." : "Try another search or scope."}
            icon={<Network size={20} />}
            title={isTr ? "Takim bulunamadi" : "No teams found"}
          />
        ) : null}
      </div>
      {(result?.totalCount ?? 0) > (result?.pageSize ?? 6) ? (
        <PaginationControls
          currentPage={result?.page ?? 1}
          language={language}
          onNext={() => onPageChange(Math.min(totalPages, (result?.page ?? 1) + 1))}
          onPageChange={onPageChange}
          onPrevious={() => onPageChange(Math.max(1, (result?.page ?? 1) - 1))}
          totalPages={totalPages}
        />
      ) : null}
    </>
  );
}

export function TeamMemberList({
  canManage,
  isLoading,
  language,
  onPageChange,
  onRemove,
  onToggleLead,
  result,
}: {
  canManage: boolean;
  isLoading: boolean;
  language: Language;
  onPageChange: (page: number) => void;
  onRemove: (member: TeamMember) => void;
  onToggleLead: (member: TeamMember) => void;
  result: TeamMemberPage | null;
}) {
  const isTr = language === "tr";
  if (isLoading && !result) return <PeopleListSkeleton />;
  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / Math.max(1, result?.pageSize ?? 6)));
  return (
    <>
      <div className="team-people-list">
        {(result?.items ?? []).map((member) => (
          <article className="team-person-row" key={member.userId}>
            <span className={member.isLead ? "team-person-avatar is-lead" : "team-person-avatar"}>{member.displayName.slice(0, 1).toUpperCase()}</span>
            <div className="team-person-copy"><strong>{member.displayName}</strong><small>@{member.username} · {member.communityRoleName || (isTr ? "Atanmadi" : "Unassigned")}</small></div>
            {member.isLead ? <span className="team-lead-pill"><Crown size={13} /> {isTr ? "Sorumlu" : "Lead"}</span> : null}
            {canManage ? (
              <div className="team-person-actions">
                <button className="text-button" onClick={() => onToggleLead(member)} type="button">{member.isLead ? (isTr ? "Sorumlulugu kaldir" : "Remove lead") : (isTr ? "Sorumlu yap" : "Make lead")}</button>
                <button className="text-button danger-text-button" onClick={() => onRemove(member)} type="button">{isTr ? "Cikar" : "Remove"}</button>
              </div>
            ) : null}
          </article>
        ))}
        {!result?.items.length ? <EmptyState description={isTr ? "Bu takimda henuz aktif uye yok." : "This team has no active members yet."} icon={<UsersRound size={20} />} title={isTr ? "Uye bulunamadi" : "No members"} /> : null}
      </div>
      {(result?.totalCount ?? 0) > (result?.pageSize ?? 6) ? <PaginationControls currentPage={result?.page ?? 1} language={language} onNext={() => onPageChange(Math.min(totalPages, (result?.page ?? 1) + 1))} onPageChange={onPageChange} onPrevious={() => onPageChange(Math.max(1, (result?.page ?? 1) - 1))} totalPages={totalPages} /> : null}
    </>
  );
}

export function TeamCandidateList({
  isLoading,
  language,
  onAdd,
  onPageChange,
  result,
}: {
  isLoading: boolean;
  language: Language;
  onAdd: (candidate: TeamCandidate) => void;
  onPageChange: (page: number) => void;
  result: TeamCandidatePage | null;
}) {
  const isTr = language === "tr";
  if (isLoading && !result) return <PeopleListSkeleton />;
  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / Math.max(1, result?.pageSize ?? 6)));
  return (
    <>
      <div className="team-people-list candidate-list">
        {(result?.items ?? []).map((candidate) => (
          <article className="team-person-row" key={candidate.userId}>
            <span className="team-person-avatar">{candidate.displayName.slice(0, 1).toUpperCase()}</span>
            <div className="team-person-copy"><strong>{candidate.displayName}</strong><small>{candidate.communityRoleName || (isTr ? "Atanmadi" : "Unassigned")} · {candidate.activeTeamCount} {isTr ? "takim" : "teams"}</small></div>
            <button aria-label={`${candidate.displayName} ${isTr ? "takima ekle" : "add to team"}`} className="icon-button team-add-member-button" onClick={() => onAdd(candidate)} title={isTr ? "Takima ekle" : "Add to team"} type="button"><UserPlus size={16} /></button>
          </article>
        ))}
        {!result?.items.length ? <EmptyState description={isTr ? "Uygun tum kullanicilar zaten bu takimda olabilir." : "All eligible users may already be in this team."} icon={<UserPlus size={20} />} title={isTr ? "Aday bulunamadi" : "No candidates"} /> : null}
      </div>
      {(result?.totalCount ?? 0) > (result?.pageSize ?? 6) ? <PaginationControls currentPage={result?.page ?? 1} language={language} onNext={() => onPageChange(Math.min(totalPages, (result?.page ?? 1) + 1))} onPageChange={onPageChange} onPrevious={() => onPageChange(Math.max(1, (result?.page ?? 1) - 1))} totalPages={totalPages} /> : null}
    </>
  );
}

export function UnassignedMemberList({ isLoading, language, result }: { isLoading: boolean; language: Language; result: TeamCandidatePage | null }) {
  const isTr = language === "tr";
  if (isLoading && !result) return <PeopleListSkeleton />;
  return (
    <div className="team-people-list">
      {(result?.items ?? []).map((candidate) => (
        <article className="team-person-row" key={candidate.userId}>
          <span className="team-person-avatar">{candidate.displayName.slice(0, 1).toUpperCase()}</span>
          <div className="team-person-copy"><strong>{candidate.displayName}</strong><small>@{candidate.username} · {candidate.communityRoleName || (isTr ? "Atanmadi" : "Unassigned")}</small></div>
        </article>
      ))}
      {!result?.items.length ? <EmptyState description={isTr ? "Bu topluluktaki tum aktif kullanicilar en az bir takima bagli." : "Every active user in this community belongs to a team."} icon={<UsersRound size={20} />} title={isTr ? "Takimsiz kullanici yok" : "No unassigned users"} /> : null}
    </div>
  );
}

function PeopleListSkeleton() {
  return <div className="team-people-list team-people-skeleton">{Array.from({ length: 4 }, (_, index) => <div className="team-person-row" key={index}><SkeletonBlock /><div><SkeletonBlock /><SkeletonBlock /></div></div>)}</div>;
}
