"use client";

import { BadgeCheck, Building2, Landmark, Pencil, Plus, RefreshCw, Tags, Trash2, Users } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ActionFeedback, InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { api } from "@/lib/api";
import type { Community, CommunityRole, CommunitySummary, Language, PermissionName, RoleTemplate, User } from "@/lib/types";
import {
  CommunityCardSkeleton,
  CommunityRolePanelSkeleton,
  ManagementConfirmation,
  permissionLabel,
  RoleCountDisclosure,
  type CommunityPendingAction,
} from "@/features/management/CommunityManagementComponents";

type Feedback = { tone: "success" | "error" | "loading"; text: string } | null;

const communitySummaryCache = new Map<string, CommunitySummary>();
const allPermissions: PermissionName[] = [
  "Community.ManageUsers",
  "Community.ManageRoles",
  "Community.ManageAdmins",
  "Forms.View",
  "Forms.Create",
  "Forms.Update",
  "Processes.View",
  "Processes.Start",
  "Tasks.View",
  "Tasks.Act",
  "Audit.View",
];

export function ManagementCommunitiesView({ activeUser, language, token }: { activeUser: User; language: Language; token: string | null }) {
  const [communities, setCommunities] = useState<Community[]>([]);
  const [roles, setRoles] = useState<CommunityRole[]>([]);
  const [templates, setTemplates] = useState<RoleTemplate[]>([]);
  const [selectedCommunityId, setSelectedCommunityId] = useState<string | null>(activeUser.communityId ?? null);
  const [summary, setSummary] = useState<CommunitySummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSummaryLoading, setIsSummaryLoading] = useState(false);
  const [isRolesLoading, setIsRolesLoading] = useState(false);
  const [rolesError, setRolesError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [pendingAction, setPendingAction] = useState<CommunityPendingAction | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [replacementRoleId, setReplacementRoleId] = useState("");
  const [createDraft, setCreateDraft] = useState({ name: "", description: "", inviteCode: "" });
  const [communityDraft, setCommunityDraft] = useState({ name: "", description: "", inviteCode: "", isActive: true });
  const [roleDraft, setRoleDraft] = useState({ name: "", description: "", templateKey: "custom", permissions: [] as PermissionName[] });
  const [templateSourceKey, setTemplateSourceKey] = useState("custom");
  const [isCustomizingTemplate, setIsCustomizingTemplate] = useState(false);
  const [communityFeedback, setCommunityFeedback] = useState<Feedback>(null);
  const [codeFeedback, setCodeFeedback] = useState<Feedback>(null);
  const [roleFeedback, setRoleFeedback] = useState<Feedback>(null);
  const isSuperAdmin = activeUser.role === "SuperAdmin";
  const canToggleOwnCommunityStatus = !isSuperAdmin && activeUser.permissions.includes("Community.ManageAdmins");
  const initialCommunityIdRef = useRef<string | null>(activeUser.role === "SuperAdmin" ? null : activeUser.communityId ?? null);
  const selectedCommunity = communities.find((community) => community.id === selectedCommunityId) ?? null;
  const selectedRole = roles.find((role) => role.id === selectedRoleId) ?? null;

  const customRoles = useMemo(() => roles.filter((role) => !role.isSystemRole), [roles]);

  const loadSummary = useCallback(
    async (communityId: string, force = false) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      const cached = communitySummaryCache.get(communityId);
      if (cached && !force) {
        setSummary(cached);
        return;
      }

      setIsSummaryLoading(true);
      try {
        const result = await api.getCommunitySummary(token, communityId);
        communitySummaryCache.set(communityId, result);
        setSummary(result);
      } catch (error) {
        setCodeFeedback({ tone: "error", text: localizeApiError(error, language, "Topluluk ozeti yuklenemedi.") });
      } finally {
        setIsSummaryLoading(false);
      }
    },
    [language, token],
  );

  const loadCommunity = useCallback(
    async (communityId: string, forceSummary = false) => {
      if (!token || token.startsWith("demo-")) {
        return;
      }

      setIsRolesLoading(true);
      setRolesError(null);
      const rolesRequest = api.listCommunityRoles(token, communityId)
        .then((roleResult) => {
          setRoles(roleResult);
          setSelectedRoleId((current) => (roleResult.some((role) => role.id === current) ? current : null));
        })
        .catch((error) => {
          setRoles([]);
          setRolesError(localizeApiError(error, language, "Topluluk rolleri yuklenemedi."));
        })
        .finally(() => setIsRolesLoading(false));
      await Promise.all([rolesRequest, loadSummary(communityId, forceSummary)]);
    },
    [language, loadSummary, token],
  );

  const loadInitial = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    setIsLoading(true);
    try {
      const communityResult = await api.listCommunities(token);
      setCommunities(communityResult);
      try {
        setTemplates(await api.listRoleTemplates(token));
      } catch (error) {
        setRoleFeedback({ tone: "error", text: localizeApiError(error, language, "Rol sablonlari yuklenemedi.") });
      }
      const nextCommunityId = initialCommunityIdRef.current ?? (isSuperAdmin ? communityResult[0]?.id ?? null : activeUser.communityId ?? null);
      initialCommunityIdRef.current = nextCommunityId;
      setSelectedCommunityId(nextCommunityId);
      if (nextCommunityId) {
        await loadCommunity(nextCommunityId);
      }
    } catch (error) {
      setCommunityFeedback({ tone: "error", text: localizeApiError(error, language, "Topluluklar yuklenemedi.") });
    } finally {
      setIsLoading(false);
    }
  }, [activeUser.communityId, isSuperAdmin, language, loadCommunity, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadInitial(), 0);
    return () => window.clearTimeout(timer);
  }, [loadInitial]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (!selectedCommunity) {
        return;
      }
      setCommunityDraft({
        name: selectedCommunity.name,
        description: selectedCommunity.description,
        inviteCode: selectedCommunity.inviteCode,
        isActive: selectedCommunity.isActive,
      });
      setCodeFeedback(null);
    }, 0);
    return () => window.clearTimeout(timer);
  }, [selectedCommunity]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (!selectedRole) {
        return;
      }
      setTemplateSourceKey(`role:${selectedRole.id}`);
      setIsCustomizingTemplate(false);
      setRoleDraft({
        name: selectedRole.name,
        description: selectedRole.description,
        templateKey: selectedRole.templateKey,
        permissions: selectedRole.permissions,
      });
      setReplacementRoleId(getUnassignedRoleId(roles) ?? "");
      setRoleFeedback(null);
    }, 0);
    return () => window.clearTimeout(timer);
  }, [roles, selectedRole]);

  useEffect(() => {
    if (!toast) {
      return;
    }
    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function selectCommunity(communityId: string) {
    setSelectedCommunityId(communityId);
    setSummary(communitySummaryCache.get(communityId) ?? null);
    setRoles([]);
    setSelectedRoleId(null);
    setTemplateSourceKey("custom");
    setIsCustomizingTemplate(false);
    setRoleDraft({ name: "", description: "", templateKey: "custom", permissions: [] });
    try {
      await loadCommunity(communityId);
    } catch (error) {
      setCommunityFeedback({ tone: "error", text: localizeApiError(error, language, "Topluluk yuklenemedi.") });
    }
  }

  async function refresh() {
    const startedAt = Date.now();
    setIsRefreshing(true);
    try {
      if (selectedCommunityId) {
        communitySummaryCache.delete(selectedCommunityId);
        await loadCommunity(selectedCommunityId, true);
      }
      await waitForMinimumDelay(startedAt, 500);
      setToast({ kind: "success", text: "Topluluk verileri yenilendi." });
    } catch (error) {
      await waitForMinimumDelay(startedAt, 500);
      setToast({ kind: "error", text: localizeApiError(error, language, "Topluluk verileri yenilenemedi.") });
    } finally {
      setIsRefreshing(false);
    }
  }

  async function executePendingAction() {
    if (!pendingAction || !token) {
      return;
    }

    const action = pendingAction;
    setPendingAction(null);
    try {
      if (action.type === "create-community") {
        setCommunityFeedback({ tone: "loading", text: "Topluluk olusturuluyor..." });
        const created = await api.createCommunity(token, {
          name: createDraft.name,
          description: createDraft.description,
          inviteCode: createDraft.inviteCode || undefined,
          isActive: true,
        });
        setCommunities((items) => [...items, created].sort((left, right) => left.name.localeCompare(right.name)));
        setCreateDraft({ name: "", description: "", inviteCode: "" });
        await selectCommunity(created.id);
        setCommunityFeedback({ tone: "success", text: `${created.name} olusturuldu.` });
        return;
      }

      if (!selectedCommunity) {
        return;
      }

      if (action.type === "update-community") {
        setCommunityFeedback({ tone: "loading", text: "Topluluk ayarlari kaydediliyor..." });
        const updated = await api.updateCommunity(token, selectedCommunity.id, communityDraft);
        setCommunities((items) => items.map((item) => (item.id === updated.id ? updated : item)));
        communitySummaryCache.delete(updated.id);
        setCommunityFeedback({
          tone: "success",
          text: updated.isActive ? "Topluluk ayarlari kaydedildi." : "Topluluk pasife alindi ve oturumlar kapatildi.",
        });
        return;
      }

      if (action.type === "regenerate-code") {
        setCodeFeedback({ tone: "loading", text: "Yeni topluluk kodu uretiliyor..." });
        const updated = await api.regenerateCommunityInviteCode(token, selectedCommunity.id);
        setCommunities((items) => items.map((item) => (item.id === updated.id ? updated : item)));
        setCodeFeedback({ tone: "success", text: `Yeni topluluk kodu: ${updated.inviteCode}` });
        return;
      }

      if (action.type === "create-role") {
        setRoleFeedback({ tone: "loading", text: "Rol olusturuluyor..." });
        const created = await api.createCommunityRole(token, selectedCommunity.id, roleDraft);
        setRoles((items) => [...items, created].sort((left, right) => left.name.localeCompare(right.name)));
        communitySummaryCache.delete(selectedCommunity.id);
        setSelectedRoleId(created.id);
        setRoleFeedback({ tone: "success", text: `${created.name} rolu olusturuldu.` });
        return;
      }

      if (action.type === "update-role") {
        setRoleFeedback({ tone: "loading", text: "Rol guncelleniyor..." });
        const updated = await api.updateCommunityRole(token, selectedCommunity.id, action.roleId, roleDraft);
        setRoles((items) => items.map((item) => (item.id === updated.id ? updated : item)));
        communitySummaryCache.delete(selectedCommunity.id);
        setRoleFeedback({ tone: "success", text: `${updated.name} rolu guncellendi.` });
        return;
      }

      if (action.type === "delete-role") {
        setRoleFeedback({ tone: "loading", text: "Rol tasiniyor ve siliniyor..." });
        await api.deleteCommunityRole(token, selectedCommunity.id, action.roleId, replacementRoleId);
        setRoles((items) => items.filter((item) => item.id !== action.roleId));
        communitySummaryCache.delete(selectedCommunity.id);
        setSelectedRoleId(null);
        setTemplateSourceKey("custom");
        setIsCustomizingTemplate(false);
        setRoleDraft({ name: "", description: "", templateKey: "custom", permissions: [] });
        await loadSummary(selectedCommunity.id, true);
        setRoleFeedback({ tone: "success", text: "Rol silindi; kullanicilar hedef role tasindi." });
      }
    } catch (error) {
      const message = localizeApiError(error, language, "Islem tamamlanamadi.");
      if (action.type === "regenerate-code") {
        setCodeFeedback({ tone: "error", text: message });
      } else if (action.type.includes("role")) {
        setRoleFeedback({ tone: "error", text: message });
      } else {
        setCommunityFeedback({ tone: "error", text: message });
      }
    }
  }

  function applyTemplate(templateKey: string) {
    const template = templates.find((item) => item.key === templateKey);
    setTemplateSourceKey(templateKey);
    setIsCustomizingTemplate(templateKey === "custom");
    setRoleDraft({
      name: templateKey === "custom" ? "" : template?.name ?? "",
      description: template?.description ?? "",
      templateKey,
      permissions: template?.permissions ?? [],
    });
  }

  function selectRoleSource(source: string) {
    if (source.startsWith("role:")) {
      setSelectedRoleId(source.slice("role:".length));
      return;
    }

    setSelectedRoleId(null);
    applyTemplate(source);
  }

  function toggleRolePermission(permission: PermissionName, checked: boolean) {
    if (!selectedRole && templateSourceKey !== "custom" && !isCustomizingTemplate) {
      setIsCustomizingTemplate(true);
      setRoleDraft((draft) => ({
        ...draft,
        name: draft.name.endsWith("*") ? draft.name : `${draft.name}*`,
        templateKey: "custom",
        permissions: checked
          ? [...draft.permissions, permission]
          : draft.permissions.filter((item) => item !== permission),
      }));
      return;
    }

    setRoleDraft((draft) => ({
      ...draft,
      permissions: checked
        ? [...draft.permissions, permission]
        : draft.permissions.filter((item) => item !== permission),
    }));
  }

  const activeRoleCount = summary?.roleCounts.find((role) => role.communityRoleId === selectedRoleId)?.userCount ?? 0;

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Yonetim</span>
          <h2>Topluluklar</h2>
        </div>
        <div className="section-heading-actions">
          <p>Topluluk davet kodlarini, durumunu ve topluluga ozel rol setlerini yonetin.</p>
          <button className="secondary-button refresh-button" disabled={isRefreshing} type="button" onClick={refresh}>
            <RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={17} />
            {isRefreshing ? "Yenileniyor" : "Yenile"}
          </button>
        </div>
      </div>

      <div className="community-management-layout">
        <div className="community-management-left">
          {isSuperAdmin ? (
            <section className="identity-section">
              <div className="section-toolbar"><div><span className="eyebrow">Platform</span><h3>Topluluk olustur</h3></div><Building2 size={22} /></div>
              <div className="compact-form community-create-form">
                <input value={createDraft.name} onChange={(event) => setCreateDraft((draft) => ({ ...draft, name: event.target.value }))} placeholder="Topluluk adi" />
                <input value={createDraft.description} onChange={(event) => setCreateDraft((draft) => ({ ...draft, description: event.target.value }))} placeholder="Aciklama" />
                <input value={createDraft.inviteCode} maxLength={5} onChange={(event) => setCreateDraft((draft) => ({ ...draft, inviteCode: event.target.value.toUpperCase() }))} placeholder="Davet kodu (opsiyonel)" />
                <button className="success-button" disabled={!createDraft.name.trim()} type="button" onClick={() => setPendingAction({ type: "create-community" })}>Topluluk olustur</button>
              </div>
              <ActionFeedback feedback={communityFeedback} />
            </section>
          ) : null}

          <section className="identity-section">
            <div className="section-toolbar"><div><span className="eyebrow">Topluluk</span><h3>{selectedCommunity?.name ?? "Topluluk yukleniyor"}</h3></div><Landmark size={22} /></div>
            {isSuperAdmin ? (
              <label className="filter-select-field compact-filter-field">
                <Building2 size={16} />
                <select value={selectedCommunityId ?? ""} onChange={(event) => void selectCommunity(event.target.value)}>
                  {communities.map((community) => <option key={community.id} value={community.id}>{community.name}</option>)}
                </select>
              </label>
            ) : null}
            {isLoading || !selectedCommunity ? (
              <CommunityCardSkeleton />
            ) : (
              <div className="community-settings-grid">
                <label><span>Topluluk adi</span><input value={communityDraft.name} disabled={!isSuperAdmin} onChange={(event) => setCommunityDraft((draft) => ({ ...draft, name: event.target.value }))} /></label>
                <label><span>Aciklama</span><input value={communityDraft.description} disabled={!isSuperAdmin} onChange={(event) => setCommunityDraft((draft) => ({ ...draft, description: event.target.value }))} /></label>
                <label><span>Kayit kodu</span><input value={communityDraft.inviteCode} disabled={!isSuperAdmin} maxLength={5} onChange={(event) => setCommunityDraft((draft) => ({ ...draft, inviteCode: event.target.value.toUpperCase() }))} /></label>
                {isSuperAdmin ? (
                  <label><span>Durum</span><select value={communityDraft.isActive ? "active" : "inactive"} onChange={(event) => setCommunityDraft((draft) => ({ ...draft, isActive: event.target.value === "active" }))}><option value="active">Aktif</option><option value="inactive">Pasif</option></select></label>
                ) : (
                  <div className="community-readonly-status"><span>Durum</span><strong className={communityDraft.isActive ? "status-pill status-active" : "status-pill status-rejected"}>{communityDraft.isActive ? "Aktif" : "Pasif"}</strong></div>
                )}
              </div>
            )}
            <div className="community-stat-grid">
              <article className="settings-row community-stat-card"><Users className="community-stat-icon" size={18} /><span>Uyeler</span><strong>{isLoading || isSummaryLoading || !summary ? <InlineValueLoader /> : summary.memberCount}</strong><small>Aktif topluluk uyeligi</small></article>
              <article className="settings-row community-stat-card"><Tags className="community-stat-icon" size={18} /><span>Rol sayisi</span><strong>{isLoading || isRolesLoading ? <InlineValueLoader /> : roles.length}</strong><small>Tanimli topluluk rolu</small></article>
            </div>
            {summary ? <RoleCountDisclosure summary={summary} /> : null}
            {isSuperAdmin ? (
              <div className="section-actions">
                <button className="secondary-button" type="button" onClick={() => setPendingAction({ type: "regenerate-code" })}>Yeni kod uret</button>
                <button className={communityDraft.isActive ? "primary-button" : "success-button"} type="button" onClick={() => setPendingAction({ type: "update-community" })}>Degisikligi uygula</button>
              </div>
            ) : null}
            {canToggleOwnCommunityStatus && selectedCommunity ? (
              <div className="section-actions community-status-action">
                <button
                  className={selectedCommunity.isActive ? "danger-button" : "success-button"}
                  type="button"
                  onClick={() => {
                    setCommunityDraft((draft) => ({ ...draft, isActive: !selectedCommunity.isActive }));
                    setPendingAction({ type: "update-community" });
                  }}
                >
                  {selectedCommunity.isActive ? "Toplulugu pasife al" : "Toplulugu aktif et"}
                </button>
              </div>
            ) : null}
            <ActionFeedback feedback={codeFeedback ?? (!isSuperAdmin ? communityFeedback : null)} />
          </section>
        </div>

        <section className="identity-section community-role-panel">
          <div className="section-toolbar"><div><span className="eyebrow">Yetkiler</span><h3>{selectedCommunity ? `${selectedCommunity.name} rolleri` : "Topluluk rolleri"}</h3></div><Tags size={22} /></div>
          {isLoading ? <CommunityRolePanelSkeleton /> : <>
          <label className="filter-select-field compact-filter-field"><BadgeCheck size={16} /><select value={templateSourceKey} onChange={(event) => selectRoleSource(event.target.value)}><option value="custom">Ozel rol olustur</option><optgroup label="Hazir roller">{templates.filter((template) => template.key !== "custom").map((template) => <option key={template.key} value={template.key}>{template.name}</option>)}</optgroup>{customRoles.length ? <optgroup label="Topluluga ozel roller">{customRoles.map((role) => <option key={role.id} value={`role:${role.id}`}>{role.name}</option>)}</optgroup> : null}</select></label>
          {isRolesLoading ? <div className="role-panel-loading"><InlineValueLoader label="Roller yukleniyor" /></div> : null}
          {rolesError ? <ActionFeedback feedback={{ tone: "error", text: rolesError }} /> : null}
          {!isRolesLoading && !rolesError && selectedCommunity && !roles.length ? <p className="status-line">Bu toplulukta rol bulunamadi. Yenile ile tekrar deneyin.</p> : null}
          <div className="role-editor-grid">
            <input value={roleDraft.name} onChange={(event) => setRoleDraft((draft) => ({ ...draft, name: event.target.value }))} placeholder="Rol adi" disabled={!selectedRole && templateSourceKey !== "custom" && !isCustomizingTemplate} />
            <input value={roleDraft.description} onChange={(event) => setRoleDraft((draft) => ({ ...draft, description: event.target.value }))} placeholder="Rol aciklamasi" disabled={!selectedRole && templateSourceKey !== "custom" && !isCustomizingTemplate} />
          </div>
          {!selectedRole && templateSourceKey !== "custom" && !isCustomizingTemplate ? <p className="helper-copy">Hazir rol <strong>{roleDraft.name}</strong> sistemde tanimlidir. Bir izin degistirerek <strong>{roleDraft.name}*</strong> adli ozel kopya olusturabilirsiniz.</p> : null}
          {!selectedRole && isCustomizingTemplate ? <p className="helper-copy">Bu kopya <strong>{roleDraft.name}</strong> adiyla ozel rol olarak kaydedilecek.</p> : null}
          <div className="permission-chip-grid">
            {allPermissions.map((permission) => <label className="checkbox-line compact-password-toggle" key={permission}><input type="checkbox" checked={roleDraft.permissions.includes(permission)} onChange={(event) => toggleRolePermission(permission, event.target.checked)} /><span>{permissionLabel(permission)}</span></label>)}
          </div>
          <div className="section-actions role-editor-actions">
            {!selectedRole && (templateSourceKey === "custom" || isCustomizingTemplate) ? <button className="success-button" disabled={!roleDraft.name.trim()} type="button" onClick={() => setPendingAction({ type: "create-role" })}><Plus size={16} /> Rol olustur</button> : null}
            {selectedRole && !selectedRole.isSystemRole ? <button className="primary-button" disabled={!roleDraft.name.trim()} type="button" onClick={() => setPendingAction({ type: "update-role", roleId: selectedRole.id })}><Pencil size={16} /> Rolu guncelle</button> : null}
            {selectedRole && !selectedRole.isSystemRole ? <button className="danger-button" type="button" onClick={() => setPendingAction({ type: "delete-role", roleId: selectedRole.id })}><Trash2 size={16} /> Rolu sil</button> : null}
          </div>
          {selectedRole ? <p className="helper-copy">Bu rolde {isSummaryLoading ? <InlineValueLoader /> : activeRoleCount} aktif kullanici bulunuyor.</p> : null}
          <ActionFeedback feedback={roleFeedback} />
          </>}
        </section>
      </div>

      {pendingAction ? <ManagementConfirmation action={pendingAction} isDeactivating={!communityDraft.isActive} selectedRole={selectedRole} roles={roles} replacementRoleId={replacementRoleId} setReplacementRoleId={setReplacementRoleId} onCancel={() => setPendingAction(null)} onConfirm={() => void executePendingAction()} /> : null}
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function getUnassignedRoleId(roles: CommunityRole[]) {
  return roles.find((role) => role.templateKey === "unassigned")?.id ?? roles[0]?.id ?? "";
}

function waitForMinimumDelay(startedAt: number, minimumMs: number) {
  const remaining = Math.max(0, minimumMs - (Date.now() - startedAt));
  return remaining ? new Promise<void>((resolve) => window.setTimeout(resolve, remaining)) : Promise.resolve();
}
