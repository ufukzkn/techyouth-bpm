"use client";

import { BriefcaseBusiness, Crown, Handshake, RefreshCw, Search, UsersRound } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { EmptyState } from "@/features/ui/EmptyState";
import { api } from "@/lib/api";
import type { Language, PagedResult, ProcessTask, TeamRosterMember, TeamRosterPage, User, UserTeamMembership } from "@/lib/types";

const rosterPageSize = 8;
const membershipCache = new Map<string, UserTeamMembership[]>();
const rosterCache = new Map<string, TeamRosterPage>();
const memberTaskCache = new Map<string, PagedResult<ProcessTask>>();

export function MyTeamsView({ activeUser, language, token }: { activeUser: User; language: Language; token: string | null }) {
  const isTr = language === "tr";
  const router = useRouter();
  const [memberships, setMemberships] = useState<UserTeamMembership[]>(membershipCache.get(activeUser.id) ?? []);
  const [selectedTeamId, setSelectedTeamId] = useState<string | null>(memberships[0]?.teamId ?? null);
  const [roster, setRoster] = useState<TeamRosterPage | null>(null);
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [isLoadingTeams, setIsLoadingTeams] = useState(!membershipCache.has(activeUser.id));
  const [isLoadingRoster, setIsLoadingRoster] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [selectedWorkloadMember, setSelectedWorkloadMember] = useState<TeamRosterMember | null>(null);
  const [memberTasks, setMemberTasks] = useState<PagedResult<ProcessTask> | null>(null);
  const [memberTaskPage, setMemberTaskPage] = useState(1);
  const [isLoadingMemberTasks, setIsLoadingMemberTasks] = useState(false);

  const selectedMembership = memberships.find((membership) => membership.teamId === selectedTeamId) ?? null;
  const rosterKey = useMemo(() => `${activeUser.id}:${selectedTeamId ?? "none"}:${query.trim()}:${page}`, [activeUser.id, page, query, selectedTeamId]);
  const canInspectTeamWorkload = activeUser.role === "SuperAdmin"
    || activeUser.permissions.includes("Teams.Manage")
    || selectedMembership?.isLead === true;

  const loadMemberships = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-")) {
      const demo = activeUser.teams?.map((team) => ({ teamId: team.id, teamName: team.name, teamIsActive: true, isLead: team.isLead, joinedAt: "" })) ?? [];
      setMemberships(demo);
      setSelectedTeamId((current) => current ?? demo[0]?.teamId ?? null);
      setIsLoadingTeams(false);
      return;
    }
    const cached = membershipCache.get(activeUser.id);
    if (cached && !force) {
      setMemberships(cached);
      setSelectedTeamId((current) => current ?? cached[0]?.teamId ?? null);
      setIsLoadingTeams(false);
      return;
    }
    setIsLoadingTeams(!cached);
    const result = await api.listUserTeamMemberships(token, activeUser.id);
    membershipCache.set(activeUser.id, result);
    setMemberships(result);
    setSelectedTeamId((current) => result.some((item) => item.teamId === current) ? current : result[0]?.teamId ?? null);
    setIsLoadingTeams(false);
  }, [activeUser, token]);

  const loadRoster = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-") || !selectedTeamId) {
      setRoster(null);
      return;
    }
    const cached = rosterCache.get(rosterKey);
    if (cached && !force) {
      setRoster(cached);
      return;
    }
    setIsLoadingRoster(true);
    try {
      const result = await api.listTeamRoster(token, selectedTeamId, { query, page, pageSize: rosterPageSize });
      rosterCache.set(rosterKey, result);
      setRoster(result);
    } finally {
      setIsLoadingRoster(false);
    }
  }, [page, query, rosterKey, selectedTeamId, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadMemberships().catch((error) => setToast({ kind: "error", text: localizeApiError(error, language, isTr ? "Takımlar yüklenemedi." : "Teams could not be loaded.") }));
    }, 0);
    return () => window.clearTimeout(timer);
  }, [isTr, language, loadMemberships]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadRoster().catch((error) => setToast({ kind: "error", text: localizeApiError(error, language, isTr ? "Takım arkadaşları yüklenemedi." : "Teammates could not be loaded.") }));
    }, 180);
    return () => window.clearTimeout(timer);
  }, [isTr, language, loadRoster]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3500);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function refresh() {
    setIsRefreshing(true);
    try {
      for (const key of rosterCache.keys()) if (key.startsWith(`${activeUser.id}:`)) rosterCache.delete(key);
      memberTaskCache.clear();
      await loadMemberships(true);
      await loadRoster(true);
      setToast({ kind: "success", text: isTr ? "Takım bilgileri yenilendi." : "Team data refreshed." });
    } catch (error) {
      setToast({ kind: "error", text: localizeApiError(error, language, isTr ? "Takım bilgileri yenilenemedi." : "Team data could not be refreshed.") });
    } finally {
      setIsRefreshing(false);
    }
  }

  async function selectMemberWorkload(member: TeamRosterMember, nextPage = 1) {
    if (!token || !selectedTeamId || !canInspectTeamWorkload || member.activeTaskCount <= 0) return;
    const cacheKey = `${selectedTeamId}:${member.userId}:${nextPage}`;
    setSelectedWorkloadMember(member);
    setMemberTaskPage(nextPage);
    const cached = memberTaskCache.get(cacheKey);
    if (cached) {
      setMemberTasks(cached);
      return;
    }

    setMemberTasks(null);
    setIsLoadingMemberTasks(true);
    try {
      const result = await api.listTeamMemberTasks(token, selectedTeamId, member.userId, {
        page: nextPage,
        pageSize: 5,
      });
      memberTaskCache.set(cacheKey, result);
      setMemberTasks(result);
    } catch (error) {
      setToast({
        kind: "error",
        text: localizeApiError(error, language, isTr ? "Üyenin işleri yüklenemedi." : "Member tasks could not be loaded."),
      });
    } finally {
      setIsLoadingMemberTasks(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil((roster?.totalCount ?? 0) / rosterPageSize));

  return (
    <section className="settings-panel my-teams-page">
      <div className="section-heading">
        <div><span className="eyebrow">{activeUser.communityName}</span><h2>{isTr ? "Takım" : "Team"}</h2><p>{isTr ? "Dahil olduğunuz operasyon takımlarını ve takım arkadaşlarınızı görüntüleyin." : "View your operational teams and teammates."}</p></div>
        <button className="secondary-button refresh-button" disabled={isRefreshing} onClick={() => void refresh()} type="button"><RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={16} />{isTr ? "Yenile" : "Refresh"}</button>
      </div>

      {isLoadingTeams ? <div className="my-teams-loading"><SkeletonBlock className="my-team-card-skeleton" /><SkeletonBlock className="my-team-card-skeleton" /></div> : !memberships.length ? (
        <EmptyState description={isTr ? "Topluluk yöneticiniz sizi bir takıma eklediğinde takım arkadaşlarınız burada görünecek." : "Your teammates will appear here after a community administrator assigns you to a team."} icon={<Handshake size={22} />} title={isTr ? "Henüz bir takıma atanmadınız" : "You are not assigned to a team yet"} />
      ) : (
        <div className="my-teams-layout">
          <section className="identity-section my-team-selector">
            <div className="section-toolbar"><div><span className="eyebrow">{isTr ? "Üyelikler" : "Memberships"}</span><h3>{isTr ? "Takımlar" : "Teams"}</h3></div><Handshake size={20} /></div>
            <div className="my-team-list">
              {memberships.map((membership) => <button className={membership.teamId === selectedTeamId ? "my-team-option is-active" : "my-team-option"} key={membership.teamId} onClick={() => { setSelectedTeamId(membership.teamId); setPage(1); setQuery(""); setSelectedWorkloadMember(null); setMemberTasks(null); }} type="button"><span><UsersRound size={17} />{membership.teamName}</span>{membership.isLead ? <small><Crown size={13} /> {isTr ? "Sorumlu" : "Lead"}</small> : null}</button>)}
            </div>
          </section>

          <section className="identity-section my-team-roster">
            <div className="section-toolbar"><div><span className="eyebrow">{isTr ? "Takım arkadaşları" : "Teammates"}</span><h3>{selectedMembership?.teamName}</h3></div><span className="team-roster-count"><UsersRound size={15} /> {roster?.totalCount ?? 0}</span></div>
            <label className="search-field"><Search size={15} /><input aria-label={isTr ? "Takım arkadaşlarında ara" : "Search teammates"} onChange={(event) => { setQuery(event.target.value); setPage(1); }} placeholder={isTr ? "Kullanıcı ara" : "Search users"} value={query} /></label>
            {isLoadingRoster && !roster ? <div className="team-roster-skeleton"><SkeletonBlock className="team-roster-row-skeleton" /><SkeletonBlock className="team-roster-row-skeleton" /><SkeletonBlock className="team-roster-row-skeleton" /></div> : null}
            {!isLoadingRoster && roster?.items.length === 0 ? <p className="status-line">{isTr ? "Bu takımda görüntülenecek aktif üye yok." : "This team has no active members to display."}</p> : null}
            <div className={isLoadingRoster ? "team-roster-list is-refreshing" : "team-roster-list"}>
              {roster?.items.map((member) => (
                <article className="settings-row team-roster-row" key={member.userId}>
                  <div className="stacked-summary"><span>@{member.username}</span><strong>{member.displayName}</strong><small>{member.communityRoleName || (isTr ? "Rol atanmadı" : "No role assigned")}</small></div>
                  <div className="team-roster-member-meta">
                    {member.isLead ? <span className="status-pill status-pending"><Crown size={13} /> {isTr ? "Sorumlu" : "Lead"}</span> : null}
                    {canInspectTeamWorkload && member.activeTaskCount > 0 ? (
                      <button className="team-member-task-count" onClick={() => void selectMemberWorkload(member)} type="button">
                        <BriefcaseBusiness size={14} /> {member.activeTaskCount} {isTr ? "aktif iş" : "active tasks"}
                      </button>
                    ) : (
                      <span className="team-member-task-count is-readonly">
                        <BriefcaseBusiness size={14} /> {member.activeTaskCount} {isTr ? "aktif iş" : "active tasks"}
                      </span>
                    )}
                  </div>
                </article>
              ))}
            </div>
            {totalPages > 1 ? <PaginationControls currentPage={page} language={language} onNext={() => setPage((value) => Math.min(totalPages, value + 1))} onPageChange={setPage} onPrevious={() => setPage((value) => Math.max(1, value - 1))} totalPages={totalPages} /> : null}
            {selectedWorkloadMember ? (
              <section className="team-member-workload">
                <div className="section-toolbar">
                  <div><span className="eyebrow">{isTr ? "İş yükü" : "Workload"}</span><h4>{selectedWorkloadMember.displayName}</h4></div>
                  <button className="text-button" onClick={() => { setSelectedWorkloadMember(null); setMemberTasks(null); }} type="button">{isTr ? "Kapat" : "Close"}</button>
                </div>
                {isLoadingMemberTasks && !memberTasks ? <div className="team-roster-skeleton"><SkeletonBlock className="team-roster-row-skeleton" /><SkeletonBlock className="team-roster-row-skeleton" /></div> : null}
                <div className="team-member-task-list">
                  {memberTasks?.items.map((task) => (
                    <button key={task.id} onClick={() => router.push(`/processes?processId=${task.processInstanceId}`)} type="button">
                      <span><strong>{task.title || task.formName}</strong><small>{task.workflowName || task.communityName}</small></span>
                      <span>{task.claimedByUserDisplayName || (isTr ? "Aday havuzu" : "Candidate pool")}</span>
                    </button>
                  ))}
                </div>
                {memberTasks && memberTasks.totalCount > memberTasks.pageSize ? (
                  <PaginationControls
                    currentPage={memberTaskPage}
                    language={language}
                    onNext={() => void selectMemberWorkload(selectedWorkloadMember, memberTaskPage + 1)}
                    onPageChange={(nextPage) => void selectMemberWorkload(selectedWorkloadMember, nextPage)}
                    onPrevious={() => void selectMemberWorkload(selectedWorkloadMember, Math.max(1, memberTaskPage - 1))}
                    totalPages={Math.max(1, Math.ceil(memberTasks.totalCount / memberTasks.pageSize))}
                  />
                ) : null}
              </section>
            ) : null}
          </section>
        </div>
      )}
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}
