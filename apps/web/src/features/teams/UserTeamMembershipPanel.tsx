"use client";

import { Crown, Network, Plus, Trash2, UsersRound } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ActionFeedback, InlineValueLoader, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { DisclosureSection } from "@/features/app-shell/components/DisclosureSection";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { api } from "@/lib/api";
import type { Language, Team, User, UserAdmin, UserTeamMembership } from "@/lib/types";

type PendingAction =
  | { type: "add"; team: Team }
  | { type: "lead"; membership: UserTeamMembership }
  | { type: "remove"; membership: UserTeamMembership };

type Feedback = { tone: "success" | "error" | "loading"; text: string } | null;

export function UserTeamMembershipPanel({
  activeUser,
  language,
  onChanged,
  selectedUser,
  token,
}: {
  activeUser: User;
  language: Language;
  onChanged: () => void;
  selectedUser: UserAdmin;
  token: string | null;
}) {
  const isTr = language === "tr";
  const canManage = activeUser.role === "SuperAdmin"
    || (activeUser.communityId === selectedUser.communityId && activeUser.permissions.includes("Teams.Manage"));
  const [memberships, setMemberships] = useState<UserTeamMembership[]>([]);
  const [availableTeams, setAvailableTeams] = useState<Team[]>([]);
  const [selectedTeamId, setSelectedTeamId] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [isWorking, setIsWorking] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);

  const load = useCallback(async () => {
    if (!token || token.startsWith("demo-") || !selectedUser.communityId) {
      setMemberships(selectedUser.teams?.map((team) => ({
        teamId: team.id,
        teamName: team.name,
        teamIsActive: true,
        isLead: team.isLead,
        joinedAt: selectedUser.createdAt,
      })) ?? []);
      setAvailableTeams([]);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    try {
      const [membershipResult, teamResult] = await Promise.all([
        api.listUserTeamMemberships(token, selectedUser.id),
        canManage
          ? api.listTeams(token, { communityId: selectedUser.communityId, isActive: true, page: 1, pageSize: 50 })
          : Promise.resolve(null),
      ]);
      setMemberships(membershipResult);
      setAvailableTeams(teamResult?.items ?? []);
    } catch (error) {
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, isTr ? "Takim uyelikleri yuklenemedi." : "Team memberships could not be loaded."),
      });
    } finally {
      setIsLoading(false);
      setHasLoaded(true);
    }
  }, [canManage, isTr, language, selectedUser, token]);

  useEffect(() => {
    if (!isOpen || hasLoaded) return;
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [hasLoaded, isOpen, load]);

  const joinableTeams = useMemo(() => {
    const memberTeamIds = new Set(memberships.map((membership) => membership.teamId));
    return availableTeams.filter((team) => !memberTeamIds.has(team.id));
  }, [availableTeams, memberships]);

  async function executeAction() {
    if (!pendingAction || !token) return;
    const action = pendingAction;
    setPendingAction(null);
    setIsWorking(true);
    setFeedback({ tone: "loading", text: isTr ? "Takim uyeligi guncelleniyor..." : "Updating team membership..." });
    try {
      if (action.type === "add") {
        await api.addTeamMember(token, action.team.id, selectedUser.id);
      } else if (action.type === "lead") {
        await api.updateTeamMember(token, action.membership.teamId, selectedUser.id, !action.membership.isLead);
      } else {
        await api.removeTeamMember(token, action.membership.teamId, selectedUser.id);
      }
      setSelectedTeamId("");
      setFeedback({ tone: "success", text: isTr ? "Takim uyeligi guncellendi." : "Team membership updated." });
      await load();
      onChanged();
    } catch (error) {
      setFeedback({
        tone: "error",
        text: localizeApiError(error, language, isTr ? "Takim uyeligi guncellenemedi." : "Team membership could not be updated."),
      });
    } finally {
      setIsWorking(false);
    }
  }

  const selectedTeam = joinableTeams.find((team) => team.id === selectedTeamId) ?? null;
  const blockedReason = !selectedUser.communityId
    ? (isTr ? "Kullanicinin aktif bir toplulugu yok." : "The user has no active community.")
    : selectedUser.status !== "Active"
      ? (isTr ? "Yalniz aktif kullanicilar takima eklenebilir." : "Only active users can join teams.")
      : null;

  return (
    <>
      <DisclosureSection
        className="nested-identity-section user-team-membership-panel"
        description={isTr ? "Kullanicinin takimlarini goruntuleyin ve yetkiniz varsa yonetin." : "View the user's teams and manage them when authorized."}
        eyebrow={isTr ? "Organizasyon" : "Organization"}
        icon={<Network size={21} />}
        isOpen={isOpen}
        onToggle={() => setIsOpen((value) => !value)}
        title={isTr ? "Takim uyelikleri" : "Team memberships"}
      >
        {isLoading ? (
          <div className="team-membership-skeleton" aria-label={isTr ? "Takim uyelikleri yukleniyor" : "Loading team memberships"}>
            <SkeletonBlock className="team-membership-row-skeleton" />
            <SkeletonBlock className="team-membership-row-skeleton" />
          </div>
        ) : memberships.length ? (
          <div className="user-team-membership-list">
            {memberships.map((membership) => (
              <article className="settings-row user-team-membership-row" key={membership.teamId}>
                <div className="stacked-summary">
                  <span>{membership.teamIsActive ? (isTr ? "Aktif takim" : "Active team") : (isTr ? "Pasif takim" : "Inactive team")}</span>
                  <strong><UsersRound size={15} /> {membership.teamName}</strong>
                  {membership.isLead ? <small className="team-lead-label"><Crown size={13} /> {isTr ? "Takim sorumlusu" : "Team lead"}</small> : null}
                </div>
                {canManage ? (
                  <div className="compact-icon-actions">
                    <button
                      className="icon-button"
                      disabled={isWorking}
                      onClick={() => setPendingAction({ type: "lead", membership })}
                      title={membership.isLead ? (isTr ? "Sorumlulugu kaldir" : "Remove lead") : (isTr ? "Sorumlu yap" : "Make lead")}
                      type="button"
                    >
                      <Crown size={16} />
                    </button>
                    <button
                      className="icon-button danger-icon-button"
                      disabled={isWorking}
                      onClick={() => setPendingAction({ type: "remove", membership })}
                      title={isTr ? "Takimdan cikar" : "Remove from team"}
                      type="button"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                ) : null}
              </article>
            ))}
          </div>
        ) : (
          <p className="status-line">{isTr ? "Kullanici henuz bir takima atanmadi." : "The user has not been assigned to a team yet."}</p>
        )}

        {canManage && !blockedReason ? (
          <div className="team-membership-add-row">
            <select aria-label={isTr ? "Eklenecek takimi sec" : "Select team to add"} onChange={(event) => setSelectedTeamId(event.target.value)} value={selectedTeamId}>
              <option value="">{isTr ? "Takim secin" : "Select a team"}</option>
              {joinableTeams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}
            </select>
            <button className="success-button" disabled={!selectedTeam || isWorking} onClick={() => selectedTeam && setPendingAction({ type: "add", team: selectedTeam })} type="button">
              {isWorking ? <InlineValueLoader /> : <Plus size={16} />} {isTr ? "Takima ekle" : "Add to team"}
            </button>
          </div>
        ) : blockedReason ? <p className="helper-copy">{blockedReason}</p> : null}
        <ActionFeedback feedback={feedback} />
      </DisclosureSection>

      {pendingAction ? (
        <ConfirmationDialog
          confirmLabel={pendingAction.type === "remove" ? (isTr ? "Takimdan cikar" : "Remove") : (isTr ? "Uygula" : "Apply")}
          description={pendingAction.type === "add"
            ? (isTr ? `${selectedUser.displayName}, ${pendingAction.team.name} takimina eklenecek.` : `${selectedUser.displayName} will join ${pendingAction.team.name}.`)
            : pendingAction.type === "lead"
              ? (isTr ? "Takim sorumlulugu degisecek; bu islem ek sistem yetkisi vermez." : "Team lead status will change; this grants no system permission.")
              : (isTr ? `${selectedUser.displayName}, ${pendingAction.membership.teamName} takimindan cikarilacak.` : `${selectedUser.displayName} will leave ${pendingAction.membership.teamName}.`)}
          eyebrow={isTr ? "Takim uyeligi" : "Team membership"}
          onCancel={() => setPendingAction(null)}
          onConfirm={() => void executeAction()}
          title={isTr ? "Islemi onayliyor musunuz?" : "Confirm this action?"}
          tone={pendingAction.type === "remove" ? "danger" : "primary"}
        />
      ) : null}
    </>
  );
}
