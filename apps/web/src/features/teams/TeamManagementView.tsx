"use client";

import { Building2, Network, Plus, RefreshCw, Search, UserPlus, UsersRound } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ActionFeedback, InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { DisclosureSection } from "@/features/app-shell/components/DisclosureSection";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { useSessionStore } from "@/features/session/sessionStore";
import {
  clearTeamDataCache,
  clearTeamDetailCache,
  teamCandidatePageCache,
  teamMemberPageCache,
} from "@/features/teams/teamManagementCache";
import {
  TeamCandidateList,
  TeamListPanel,
  TeamMemberList,
  UnassignedMemberList,
} from "@/features/teams/TeamManagementPanels";
import { useTeamManagement } from "@/features/teams/useTeamManagement";
import { EmptyState } from "@/features/ui/EmptyState";
import { api } from "@/lib/api";
import type {
  Language,
  Team,
  TeamCandidate,
  TeamCandidatePage,
  TeamMember,
  TeamMemberPage,
  User,
} from "@/lib/types";

const detailPageSize = 6;

type Feedback = { tone: "success" | "error" | "loading"; text: string } | null;
type PendingAction =
  | { type: "create" }
  | { type: "update" }
  | { type: "add"; candidate: TeamCandidate }
  | { type: "toggle-lead"; member: TeamMember }
  | { type: "remove"; member: TeamMember };

export function TeamManagementView({ activeUser, language, token }: { activeUser: User; language: Language; token: string | null }) {
  const isTr = language === "tr";
  const canManage = activeUser.role === "SuperAdmin" || activeUser.permissions.includes("Teams.Manage");
  const setUser = useSessionStore((state) => state.setUser);
  const [pageFeedback, setPageFeedback] = useState<Feedback>(null);
  const [createFeedback, setCreateFeedback] = useState<Feedback>(null);
  const [detailFeedback, setDetailFeedback] = useState<Feedback>(null);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const showPageError = useCallback((text: string) => setPageFeedback({ tone: "error", text }), []);
  const management = useTeamManagement({ activeUser, onError: showPageError, token });
  const [selectedTeam, setSelectedTeam] = useState<Team | null>(null);
  const [selectedTeamId, setSelectedTeamId] = useState<string | "unassigned" | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [candidatesOpen, setCandidatesOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [createDraft, setCreateDraft] = useState({ name: "", description: "" });
  const [teamDraft, setTeamDraft] = useState({ name: "", description: "", isActive: true });
  const [memberQuery, setMemberQuery] = useState("");
  const [candidateQuery, setCandidateQuery] = useState("");
  const [memberPage, setMemberPage] = useState(1);
  const [candidatePage, setCandidatePage] = useState(1);
  const [memberResult, setMemberResult] = useState<TeamMemberPage | TeamCandidatePage | null>(null);
  const [candidateResult, setCandidateResult] = useState<TeamCandidatePage | null>(null);
  const [isMembersLoading, setIsMembersLoading] = useState(false);
  const [isCandidatesLoading, setIsCandidatesLoading] = useState(false);
  const [isRefreshingDetails, setIsRefreshingDetails] = useState(false);
  const selectedCommunity = management.communities.find((community) => community.id === management.selectedCommunityId) ?? null;

  const memberCacheKey = useMemo(
    () => `${activeUser.id}:${selectedTeamId ?? "none"}:${memberQuery.trim()}:${memberPage}`,
    [activeUser.id, memberPage, memberQuery, selectedTeamId],
  );
  const candidateCacheKey = useMemo(
    () => `${activeUser.id}:${selectedTeamId ?? "none"}:${candidateQuery.trim()}:${candidatePage}`,
    [activeUser.id, candidatePage, candidateQuery, selectedTeamId],
  );

  const loadMembers = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-") || !selectedTeamId) {
      setMemberResult(null);
      return;
    }
    const cached = selectedTeamId === "unassigned"
      ? teamCandidatePageCache.get(memberCacheKey)
      : teamMemberPageCache.get(memberCacheKey);
    if (cached && !force) {
      setMemberResult(cached);
      return;
    }
    setIsMembersLoading(true);
    try {
      if (selectedTeamId === "unassigned") {
        if (!management.selectedCommunityId) return;
        const result = await api.listUnassignedTeamMembers(token, {
          communityId: management.selectedCommunityId,
          query: memberQuery,
          page: memberPage,
          pageSize: detailPageSize,
        });
        teamCandidatePageCache.set(memberCacheKey, result);
        setMemberResult(result);
      } else {
        const result = await api.listTeamMembers(token, selectedTeamId, {
          query: memberQuery,
          page: memberPage,
          pageSize: detailPageSize,
        });
        teamMemberPageCache.set(memberCacheKey, result);
        setMemberResult(result);
      }
    } catch (error) {
      setDetailFeedback({ tone: "error", text: localizeApiError(error, language, isTr ? "Uyeler yuklenemedi." : "Members could not be loaded.") });
    } finally {
      setIsMembersLoading(false);
    }
  }, [isTr, language, management.selectedCommunityId, memberCacheKey, memberPage, memberQuery, selectedTeamId, token]);

  const loadCandidates = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-") || !canManage || !selectedTeamId || selectedTeamId === "unassigned") {
      setCandidateResult(null);
      return;
    }
    const cached = teamCandidatePageCache.get(candidateCacheKey);
    if (cached && !force) {
      setCandidateResult(cached);
      return;
    }
    setIsCandidatesLoading(true);
    try {
      const result = await api.listTeamCandidates(token, selectedTeamId, {
        query: candidateQuery,
        page: candidatePage,
        pageSize: detailPageSize,
      });
      teamCandidatePageCache.set(candidateCacheKey, result);
      setCandidateResult(result);
    } catch (error) {
      setDetailFeedback({ tone: "error", text: localizeApiError(error, language, isTr ? "Uye adaylari yuklenemedi." : "Candidates could not be loaded.") });
    } finally {
      setIsCandidatesLoading(false);
    }
  }, [canManage, candidateCacheKey, candidatePage, candidateQuery, isTr, language, selectedTeamId, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadMembers(), 220);
    return () => window.clearTimeout(timer);
  }, [loadMembers]);

  useEffect(() => {
    if (!candidatesOpen) return;
    const timer = window.setTimeout(() => void loadCandidates(), 220);
    return () => window.clearTimeout(timer);
  }, [candidatesOpen, loadCandidates]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3500);
    return () => window.clearTimeout(timer);
  }, [toast]);

  function selectTeam(teamId: string) {
    const nextTeam = management.result?.items.find((team) => team.id === teamId) ?? null;
    setSelectedTeamId(teamId);
    setSelectedTeam(nextTeam);
    if (nextTeam) {
      setTeamDraft({ name: nextTeam.name, description: nextTeam.description, isActive: nextTeam.isActive });
    }
    setMemberPage(1);
    setCandidatePage(1);
    setMemberQuery("");
    setCandidateQuery("");
    setMemberResult(null);
    setCandidateResult(null);
    setCandidatesOpen(false);
    setDetailFeedback(null);
  }

  function selectUnassigned() {
    setSelectedTeamId("unassigned");
    setSelectedTeam(null);
    setMemberPage(1);
    setMemberQuery("");
    setMemberResult(null);
    setCandidatesOpen(false);
    setDetailFeedback(null);
  }

  function changeCommunity(communityId: string | null) {
    management.selectCommunity(communityId);
    setSelectedTeamId(null);
    setSelectedTeam(null);
    setMemberResult(null);
    setCandidateResult(null);
    setDetailFeedback(null);
  }

  async function refreshActiveUserIfNeeded(userId?: string) {
    if (!token || !userId || userId !== activeUser.id) return;
    setUser(await api.me(token));
  }

  async function reloadAfterMutation(teamId?: string, affectedUserId?: string) {
    clearTeamDataCache();
    if (teamId) clearTeamDetailCache(teamId);
    await management.refresh();
    if (selectedTeamId) {
      setMemberResult(null);
      setCandidateResult(null);
      await Promise.all([loadMembers(true), candidatesOpen ? loadCandidates(true) : Promise.resolve()]);
    }
    await refreshActiveUserIfNeeded(affectedUserId);
  }

  async function executePendingAction() {
    if (!pendingAction || !token) return;
    const action = pendingAction;
    setPendingAction(null);
    try {
      if (action.type === "create") {
        if (!management.selectedCommunityId) return;
        setCreateFeedback({ tone: "loading", text: isTr ? "Takim olusturuluyor..." : "Creating team..." });
        const created = await api.createTeam(token, { communityId: management.selectedCommunityId, ...createDraft });
        setCreateDraft({ name: "", description: "" });
        setCreateFeedback({ tone: "success", text: isTr ? `${created.name} olusturuldu.` : `${created.name} created.` });
        clearTeamDataCache();
        await management.refresh();
        setSelectedTeam(created);
        setSelectedTeamId(created.id);
        setTeamDraft({ name: created.name, description: created.description, isActive: created.isActive });
        return;
      }
      if (!selectedTeam || selectedTeamId === "unassigned") return;
      if (action.type === "update") {
        setDetailFeedback({ tone: "loading", text: isTr ? "Takim kaydediliyor..." : "Saving team..." });
        const updated = await api.updateTeam(token, selectedTeam.id, teamDraft);
        setSelectedTeam(updated);
        setDetailFeedback({ tone: "success", text: isTr ? "Takim bilgileri guncellendi." : "Team updated." });
        await reloadAfterMutation(updated.id);
        return;
      }
      if (action.type === "add") {
        setDetailFeedback({ tone: "loading", text: isTr ? "Kullanici takima ekleniyor..." : "Adding user..." });
        await api.addTeamMember(token, selectedTeam.id, action.candidate.userId);
        setDetailFeedback({ tone: "success", text: isTr ? `${action.candidate.displayName} takima eklendi.` : `${action.candidate.displayName} added.` });
        await reloadAfterMutation(selectedTeam.id, action.candidate.userId);
        return;
      }
      if (action.type === "toggle-lead") {
        setDetailFeedback({ tone: "loading", text: isTr ? "Sorumluluk guncelleniyor..." : "Updating lead..." });
        await api.updateTeamMember(token, selectedTeam.id, action.member.userId, !action.member.isLead);
        setDetailFeedback({ tone: "success", text: isTr ? "Takim sorumlulugu guncellendi." : "Team lead updated." });
        await reloadAfterMutation(selectedTeam.id, action.member.userId);
        return;
      }
      setDetailFeedback({ tone: "loading", text: isTr ? "Uyelik kaldiriliyor..." : "Removing membership..." });
      await api.removeTeamMember(token, selectedTeam.id, action.member.userId);
      setDetailFeedback({ tone: "success", text: isTr ? `${action.member.displayName} takimdan cikarildi.` : `${action.member.displayName} removed.` });
      await reloadAfterMutation(selectedTeam.id, action.member.userId);
    } catch (error) {
      const text = localizeApiError(error, language, isTr ? "Islem tamamlanamadi." : "Action failed.");
      if (action.type === "create") setCreateFeedback({ tone: "error", text });
      else setDetailFeedback({ tone: "error", text });
    }
  }

  async function refreshAll() {
    setIsRefreshingDetails(true);
    const startedAt = Date.now();
    try {
      clearTeamDataCache();
      await management.refresh();
      if (selectedTeamId) await Promise.all([loadMembers(true), candidatesOpen ? loadCandidates(true) : Promise.resolve()]);
      await waitForMinimumDelay(startedAt, 500);
      setToast({ kind: "success", text: isTr ? "Takim verileri yenilendi." : "Team data refreshed." });
    } catch {
      await waitForMinimumDelay(startedAt, 500);
      setToast({ kind: "error", text: isTr ? "Takim verileri yenilenemedi." : "Team data could not be refreshed." });
    } finally {
      setIsRefreshingDetails(false);
    }
  }

  return (
    <section className="settings-panel team-management-page">
      <div className="section-heading">
        <div><span className="eyebrow">{isTr ? "Yonetim" : "Management"}</span><h2>{isTr ? "Takim yonetimi" : "Team management"}</h2><p>{isTr ? "Topluluk icindeki operasyon gruplarini, uyeleri ve sorumlulari yonetin." : "Manage operational groups, members and leads inside a community."}</p></div>
        <button className="secondary-button refresh-button" disabled={isRefreshingDetails} onClick={() => void refreshAll()} type="button"><RefreshCw className={isRefreshingDetails ? "spin-icon" : undefined} size={16} />{isRefreshingDetails ? (isTr ? "Yenileniyor" : "Refreshing") : (isTr ? "Yenile" : "Refresh")}</button>
      </div>

      <section className="identity-section team-filter-card">
        <div className="team-filter-row">
          <label className="search-field"><Search size={16} /><input aria-label={isTr ? "Takimlarda ara" : "Search teams"} onChange={(event) => management.setQuery(event.target.value)} placeholder={isTr ? "Takim adi veya aciklama ara" : "Search name or description"} value={management.query} /></label>
          {activeUser.role === "SuperAdmin" ? <label className="filter-select-field"><Building2 size={16} /><select aria-label={isTr ? "Topluluk sec" : "Select community"} disabled={management.isCommunitiesLoading} onChange={(event) => changeCommunity(event.target.value || null)} value={management.selectedCommunityId ?? ""}><option value="">{isTr ? "Tum topluluklar" : "All communities"}</option>{management.communities.map((community) => <option key={community.id} value={community.id}>{community.name}</option>)}</select></label> : <strong className="team-fixed-community"><Building2 size={16} /> {activeUser.communityName}</strong>}
          <label className="filter-select-field compact-team-status"><select aria-label={isTr ? "Takim durumu" : "Team status"} onChange={(event) => management.setActiveFilter(event.target.value as "all" | "active" | "inactive")} value={management.activeFilter}><option value="all">{isTr ? "Tum durumlar" : "All statuses"}</option><option value="active">{isTr ? "Aktif" : "Active"}</option><option value="inactive">{isTr ? "Pasif" : "Inactive"}</option></select></label>
        </div>
        <div className="team-scope-summary"><span>{selectedCommunity?.name ?? (isTr ? "Platform geneli" : "Platform-wide")}</span><strong>{management.isLoading && !management.result ? <InlineValueLoader /> : `${management.result?.totalCount ?? 0} ${isTr ? "takim" : "teams"}`}</strong>{management.isRefreshing ? <InlineValueLoader label={isTr ? "Arka planda yenileniyor" : "Refreshing in background"} /> : null}</div>
        <ActionFeedback feedback={pageFeedback} />
      </section>

      <div className="team-management-layout">
        <div className="team-management-left">
          {canManage ? <DisclosureSection className="team-create-section" description={management.selectedCommunityId ? (isTr ? "Secili topluluga yeni operasyon takimi ekleyin." : "Add an operational team to the selected community.") : (isTr ? "Takim olusturmak icin once bir topluluk secin." : "Select a community before creating a team.")} eyebrow={isTr ? "Yeni kayit" : "New record"} icon={<Plus size={18} />} isOpen={createOpen} onToggle={() => setCreateOpen((value) => !value)} title={isTr ? "Takim olustur" : "Create team"}>
            <div className="compact-form team-create-form"><label><span>{isTr ? "Takim adi" : "Team name"}</span><input maxLength={80} onChange={(event) => setCreateDraft((draft) => ({ ...draft, name: event.target.value }))} value={createDraft.name} /></label><label><span>{isTr ? "Aciklama" : "Description"}</span><textarea maxLength={400} onChange={(event) => setCreateDraft((draft) => ({ ...draft, description: event.target.value }))} rows={3} value={createDraft.description} /></label><div className="section-actions"><ActionFeedback feedback={createFeedback} /><button className="success-button" disabled={!management.selectedCommunityId || !createDraft.name.trim()} onClick={() => setPendingAction({ type: "create" })} type="button"><Plus size={16} /> {isTr ? "Takim olustur" : "Create team"}</button></div></div>
          </DisclosureSection> : null}

          <section className="identity-section team-list-panel">
            <div className="section-toolbar"><div><span className="eyebrow">{isTr ? "Organizasyon" : "Organization"}</span><h3>{isTr ? "Takimlar" : "Teams"}</h3></div><Network size={21} /></div>
            <TeamListPanel language={language} isLoading={management.isLoading} onPageChange={management.setPage} onSelectTeam={selectTeam} onSelectUnassigned={selectUnassigned} result={management.result} selectedCommunityId={management.selectedCommunityId} selectedTeamId={selectedTeamId} showUnassigned={Boolean(management.selectedCommunityId)} />
          </section>
        </div>

        <section className="identity-section team-detail-panel">
          {!selectedTeamId ? <EmptyState description={isTr ? "Uyeleri ve takim ayarlarini incelemek icin soldan bir takim secin." : "Select a team to inspect members and settings."} icon={<Network size={20} />} title={isTr ? "Takim detayi" : "Team details"} /> : selectedTeamId === "unassigned" ? (
            <UnassignedDetail isLoading={isMembersLoading} language={language} memberPage={memberPage} onPageChange={setMemberPage} query={memberQuery} result={memberResult as TeamCandidatePage | null} setQuery={setMemberQuery} />
          ) : selectedTeam ? (
            <>
              <div className="section-toolbar"><div><span className="eyebrow">{selectedTeam.communityName}</span><h3>{selectedTeam.name}</h3></div><span className={selectedTeam.isActive ? "status-pill status-active" : "status-pill status-rejected"}>{selectedTeam.isActive ? (isTr ? "Aktif" : "Active") : (isTr ? "Pasif" : "Inactive")}</span></div>
              {canManage ? <div className="team-editor-grid"><label><span>{isTr ? "Takim adi" : "Team name"}</span><input maxLength={80} onChange={(event) => setTeamDraft((draft) => ({ ...draft, name: event.target.value }))} value={teamDraft.name} /></label><label><span>{isTr ? "Durum" : "Status"}</span><select onChange={(event) => setTeamDraft((draft) => ({ ...draft, isActive: event.target.value === "active" }))} value={teamDraft.isActive ? "active" : "inactive"}><option value="active">{isTr ? "Aktif" : "Active"}</option><option value="inactive">{isTr ? "Pasif" : "Inactive"}</option></select></label><label className="team-description-field"><span>{isTr ? "Aciklama" : "Description"}</span><textarea maxLength={400} onChange={(event) => setTeamDraft((draft) => ({ ...draft, description: event.target.value }))} rows={3} value={teamDraft.description} /></label><div className="section-actions team-save-actions"><ActionFeedback feedback={detailFeedback} /><button className="primary-button" disabled={!teamDraft.name.trim()} onClick={() => setPendingAction({ type: "update" })} type="button">{isTr ? "Degisikligi uygula" : "Apply changes"}</button></div></div> : <p className="helper-copy">{selectedTeam.description}</p>}
              {!canManage ? <ActionFeedback feedback={detailFeedback} /> : null}
              <div className="team-subsection-heading"><div><span className="eyebrow">{isTr ? "Uyelikler" : "Memberships"}</span><h4>{isTr ? "Takim uyeleri" : "Team members"}</h4></div><span><UsersRound size={15} /> {memberResult?.totalCount ?? selectedTeam.memberCount}</span></div>
              <label className="search-field team-member-search"><Search size={15} /><input aria-label={isTr ? "Takim uyelerinde ara" : "Search team members"} onChange={(event) => { setMemberQuery(event.target.value); setMemberPage(1); }} placeholder={isTr ? "Kullanici ara" : "Search users"} value={memberQuery} /></label>
              <TeamMemberList canManage={canManage} isLoading={isMembersLoading} language={language} onPageChange={setMemberPage} onRemove={(member) => setPendingAction({ type: "remove", member })} onToggleLead={(member) => setPendingAction({ type: "toggle-lead", member })} result={memberResult as TeamMemberPage | null} />
              {canManage ? <DisclosureSection className="team-candidate-section" description={isTr ? "Ayni topluluktaki aktif ve onayli kullanicilari takima ekleyin." : "Add active approved users from this community."} eyebrow={isTr ? "Uye ekleme" : "Membership"} icon={<UserPlus size={18} />} isOpen={candidatesOpen} onToggle={() => setCandidatesOpen((value) => !value)} title={isTr ? "Takima kullanici ekle" : "Add user to team"}><label className="search-field"><Search size={15} /><input aria-label={isTr ? "Uye adaylarinda ara" : "Search candidates"} onChange={(event) => { setCandidateQuery(event.target.value); setCandidatePage(1); }} placeholder={isTr ? "Kullanici ara" : "Search users"} value={candidateQuery} /></label><TeamCandidateList isLoading={isCandidatesLoading} language={language} onAdd={(candidate) => setPendingAction({ type: "add", candidate })} onPageChange={setCandidatePage} result={candidateResult} /></DisclosureSection> : null}
            </>
          ) : <EmptyState description={isTr ? "Takim bilgisi mevcut sayfada bulunamadi; listeyi yenileyin." : "Team data is not available on this page; refresh the list."} icon={<Network size={20} />} title={isTr ? "Takim yuklenemedi" : "Team unavailable"} />}
        </section>
      </div>

      {pendingAction ? <TeamActionConfirmation action={pendingAction} isTr={isTr} onCancel={() => setPendingAction(null)} onConfirm={() => void executePendingAction()} /> : null}
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function UnassignedDetail({ isLoading, language, memberPage, onPageChange, query, result, setQuery }: { isLoading: boolean; language: Language; memberPage: number; onPageChange: (page: number) => void; query: string; result: TeamCandidatePage | null; setQuery: (value: string) => void }) {
  const isTr = language === "tr";
  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / Math.max(1, result?.pageSize ?? detailPageSize)));
  return <><div className="section-toolbar"><div><span className="eyebrow">{isTr ? "Sanal kategori" : "Virtual category"}</span><h3>{isTr ? "Takimsiz kullanicilar" : "Unassigned users"}</h3></div><UsersRound size={21} /></div><p className="helper-copy">{isTr ? "Bu liste veritabaninda ayri bir takim degildir. Aktif takim uyeligi bulunmayan kullanicilardan otomatik hesaplanir." : "This is not a persisted team. It is calculated from users without active team memberships."}</p><label className="search-field team-member-search"><Search size={15} /><input aria-label={isTr ? "Takimsiz kullanicilarda ara" : "Search unassigned users"} onChange={(event) => { setQuery(event.target.value); onPageChange(1); }} placeholder={isTr ? "Kullanici ara" : "Search users"} value={query} /></label><UnassignedMemberList isLoading={isLoading} language={language} result={result} />{(result?.totalCount ?? 0) > (result?.pageSize ?? detailPageSize) ? <PaginationControls currentPage={memberPage} language={language} onNext={() => onPageChange(Math.min(totalPages, memberPage + 1))} onPageChange={onPageChange} onPrevious={() => onPageChange(Math.max(1, memberPage - 1))} totalPages={totalPages} /> : null}</>;
}

function TeamActionConfirmation({ action, isTr, onCancel, onConfirm }: { action: PendingAction; isTr: boolean; onCancel: () => void; onConfirm: () => void }) {
  const copy = action.type === "create"
    ? [isTr ? "Takim olustur" : "Create team", isTr ? "Yeni takim secili toplulukta kullanilabilir olacak." : "The new team will be available in the selected community.", isTr ? "Olustur" : "Create"]
    : action.type === "update"
      ? [isTr ? "Takimi guncelle" : "Update team", isTr ? "Takim bilgileri ve aktiflik durumu guncellenecek." : "Team details and status will be updated.", isTr ? "Guncelle" : "Update"]
      : action.type === "add"
        ? [isTr ? "Kullaniciyi ekle" : "Add user", isTr ? `${action.candidate.displayName} bu takimin aktif uyesi olacak.` : `${action.candidate.displayName} will become an active member.`, isTr ? "Takima ekle" : "Add"]
        : action.type === "toggle-lead"
          ? [isTr ? "Sorumlulugu guncelle" : "Update lead", action.member.isLead ? (isTr ? "Kullanicinin takim sorumlulugu kaldirilacak; diger yetkileri degismeyecek." : "Team lead status will be removed; permissions stay unchanged.") : (isTr ? "Kullanici takim sorumlusu olarak isaretlenecek; bu isaret ek yetki vermez." : "The user will be marked as team lead; this grants no extra permission."), isTr ? "Uygula" : "Apply"]
          : [isTr ? "Uyeyi cikar" : "Remove member", isTr ? `${action.member.displayName} bu takimdan cikarilacak.` : `${action.member.displayName} will be removed from this team.`, isTr ? "Takimdan cikar" : "Remove"];
  return <ConfirmationDialog confirmLabel={copy[2]} description={copy[1]} eyebrow={copy[0]} onCancel={onCancel} onConfirm={onConfirm} title={`${copy[0]}?`} tone={action.type === "remove" ? "danger" : "primary"} />;
}

function waitForMinimumDelay(startedAt: number, minimumMs: number) {
  const remaining = Math.max(0, minimumMs - (Date.now() - startedAt));
  return new Promise<void>((resolve) => window.setTimeout(resolve, remaining));
}
