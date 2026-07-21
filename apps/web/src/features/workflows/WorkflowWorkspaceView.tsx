"use client";

import { CopyPlus, Plus, RefreshCw } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type {
  Community,
  ProcessDefinition,
  ProcessDefinitionSummary,
  ProcessDefinitionVersion,
} from "@/lib/types";
import { api, ApiError } from "@/lib/api";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { useSessionStore } from "@/features/session/sessionStore";
import { Button } from "@/features/ui/Button";
import type {
  SaveWorkflowDraftRequest,
  WorkflowDefinitionDraft,
  WorkflowEditorLookups,
  WorkflowMutationResult,
} from "@/features/workflows/contracts";
import { emptyWorkflowLookups } from "@/features/workflows/contracts";
import { fromApiProcessGraph, resolveLookupLabels } from "@/features/workflows/apiGraphAdapter";
import { WorkflowEditor } from "@/features/workflows/WorkflowEditor";
import { workflowText } from "@/features/workflows/workflowI18n";
import { createStarterWorkflowDraft, getNextWorkflowName } from "@/features/workflows/workflowDraft";

type WorkspaceStatus = "loading" | "ready" | "error";

export function WorkflowWorkspaceView() {
  const token = useSessionStore((state) => state.token);
  const user = useSessionStore((state) => state.user);
  const language = useSessionStore((state) => state.language);
  const text = (tr: string, en: string) => workflowText(language, tr, en);
  const [definitions, setDefinitions] = useState<ProcessDefinitionSummary[]>([]);
  const [communities, setCommunities] = useState<Community[]>([]);
  const [selectedDefinition, setSelectedDefinition] = useState<ProcessDefinition | null>(null);
  const [selectedVersion, setSelectedVersion] = useState<ProcessDefinitionVersion | null>(null);
  const [selectedCommunityId, setSelectedCommunityId] = useState(user?.communityId ?? "");
  const [draft, setDraft] = useState<WorkflowDefinitionDraft>(() => createStarterWorkflowDraft());
  const [lookups, setLookups] = useState<WorkflowEditorLookups>(emptyWorkflowLookups);
  const [status, setStatus] = useState<WorkspaceStatus>("loading");
  const [workspaceMessage, setWorkspaceMessage] = useState(() => text("Akış tanımları yükleniyor...", "Loading workflow definitions..."));
  const [isRefreshing, setIsRefreshing] = useState(false);
  const canCreate = Boolean(user?.role === "SuperAdmin" || user?.permissions.includes("Workflows.Create"));
  const canUpdate = Boolean(user?.role === "SuperAdmin" || user?.permissions.includes("Workflows.Update"));
  const canPublish = Boolean(user?.role === "SuperAdmin" || user?.permissions.includes("Workflows.Publish"));
  const isSuperAdmin = user?.role === "SuperAdmin";
  const canCreateDefinition = canCreate && canUpdate && Boolean(selectedCommunityId);
  const canEditCurrent = selectedDefinition ? canUpdate : canCreate && canUpdate;
  const selectedDefinitionId = selectedDefinition?.id ?? "";

  async function loadLookups(communityId: string | null | undefined) {
    if (!token || !communityId) {
      setLookups(emptyWorkflowLookups);
      return emptyWorkflowLookups;
    }

    const forms = await settle(api.listForms(token), []);
    const communityForms = forms.filter((form) => form.communityId === communityId);
    const versionGroups = await Promise.all(communityForms.map((form) => settle(api.listFormVersions(token, form.id), [])));
    const [teams, people, communityRoles] = await Promise.all([
      settle(api.listTeams(token, { communityId, isActive: true, pageSize: 100 }), null),
      settle(api.listUsers(token, { communityId, status: "Active", pageSize: 100 }), null),
      settle(api.listCommunityRoles(token, communityId), []),
    ]);

    const nextLookups: WorkflowEditorLookups = {
      formVersions: versionGroups.flat()
        .filter((version) => version.status === "Published")
        .map((version) => ({
          id: version.id,
          definitionId: version.formDefinitionId,
          label: version.formName,
          description: `v${version.versionNumber}`,
          version: version.versionNumber,
          fields: version.pages.flatMap((page) => page.fields.map((field) => ({
            key: field.key,
            label: field.label,
            valueType: conditionValueType(field.type),
          }))),
        }))
        .sort((left, right) => left.label.localeCompare(right.label, language === "tr" ? "tr-TR" : "en-US") || right.version - left.version),
      teams: (teams?.items ?? []).map((team) => ({ id: team.id, label: team.name, description: team.description })),
      people: (people?.items ?? []).map((person) => ({ id: person.id, label: person.displayName, description: person.username })),
      communityRoles: communityRoles.map((role) => ({ id: role.id, label: role.name, description: role.description })),
    };
    setLookups(nextLookups);
    return nextLookups;
  }

  async function openDefinition(definitionId: string, options: { quiet?: boolean } = {}) {
    if (!token) {
      return;
    }
    if (!options.quiet) {
      setStatus("loading");
      setWorkspaceMessage(text("Akış sürümü yükleniyor...", "Loading workflow version..."));
    }

    try {
      const definition = await api.getProcessDefinition(token, definitionId);
      const version = selectEditableVersion(definition.versions);
      const nextLookups = await loadLookups(definition.communityId);
      setSelectedCommunityId(definition.communityId);
      setSelectedDefinition(definition);
      setSelectedVersion(version);
      setDraft(version
        ? addLookupLabels(versionToDraft(definition, version), nextLookups)
        : createDefinitionDraft(definition));
      setStatus("ready");
      setWorkspaceMessage(version
        ? text(
          `v${version.versionNumber} ${version.status === "Published" ? "yayın sürümü" : "taslağı"} açık.`,
          `v${version.versionNumber} ${version.status === "Published" ? "published version" : "draft"} is open.`,
        )
        : text("Tanım için ilk taslak hazır.", "The first draft is ready for this definition."));
    } catch (error) {
      setStatus("error");
      setWorkspaceMessage(toErrorMessage(error, language, text("Akış tanımı yüklenemedi.", "Workflow definition could not be loaded.")));
    }
  }

  async function loadWorkspace(manual = false) {
    if (!token) {
      setStatus("error");
      setWorkspaceMessage(text("Akışları görüntülemek için oturum gereklidir.", "A session is required to view workflows."));
      return;
    }
    if (manual) {
      setIsRefreshing(true);
    } else {
      setStatus("loading");
    }

    try {
      const [result, availableCommunities] = await Promise.all([
        api.listProcessDefinitions(token),
        isSuperAdmin ? api.listCommunities(token) : Promise.resolve([]),
      ]);
      setDefinitions(result);
      setCommunities(availableCommunities);
      const nextId = selectedDefinitionId && result.some((definition) => definition.id === selectedDefinitionId)
        ? selectedDefinitionId
        : result[0]?.id;
      if (nextId) {
        await openDefinition(nextId, { quiet: true });
      } else {
        const nextCommunityId = user?.communityId ?? availableCommunities[0]?.id ?? "";
        setSelectedDefinition(null);
        setSelectedVersion(null);
        setSelectedCommunityId(nextCommunityId);
        setDraft(createStarterWorkflowDraft(getNextWorkflowName(result)));
        await loadLookups(nextCommunityId);
        setStatus("ready");
        setWorkspaceMessage(text("Yeni akış taslağı hazır.", "The new workflow draft is ready."));
      }
    } catch (error) {
      setStatus("error");
      setWorkspaceMessage(toErrorMessage(error, language, text("Akış tanımları yüklenemedi.", "Workflow definitions could not be loaded.")));
    } finally {
      setIsRefreshing(false);
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void loadWorkspace();
    // The initial request is repeated when the authenticated session changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, user?.id]);

  useEffect(() => {
    if (status === "loading") {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setWorkspaceMessage(text("Akış tanımları yükleniyor...", "Loading workflow definitions..."));
      return;
    }
    if (status !== "ready") {
      return;
    }
    // Keep passive status text in sync when the language changes in place.
    setWorkspaceMessage(selectedVersion
      ? text(
        `v${selectedVersion.versionNumber} ${selectedVersion.status === "Published" ? "yayın sürümü" : "taslağı"} açık.`,
        `v${selectedVersion.versionNumber} ${selectedVersion.status === "Published" ? "published version" : "draft"} is open.`,
      )
      : text("Yeni akış taslağı hazır.", "The new workflow draft is ready."));
    // Only language and the active version define this passive copy.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [language, selectedVersion?.id, selectedVersion?.status, selectedVersion?.versionNumber, status]);

  async function persistWorkflow(request: SaveWorkflowDraftRequest, publish: boolean) {
    if (!token) {
      throw new Error(text("Oturum bulunamadı.", "Session not found."));
    }
    if (!request.formDefinitionVersionId) {
      throw new Error(text("Başlangıç form sürümü zorunludur.", "A start form version is required."));
    }

    let definition = selectedDefinition;
    if (!definition) {
      definition = await api.createProcessDefinition(token, {
        name: request.name,
        description: request.description,
        communityId: selectedCommunityId || undefined,
      });
      setSelectedDefinition(definition);
      setDefinitions((current) => current.some((item) => item.id === definition!.id)
        ? current
        : [definition!, ...current]);
    } else if (definition.name !== request.name || definition.description !== request.description) {
      definition = await api.updateProcessDefinition(token, definition.id, {
        name: request.name,
        description: request.description,
      });
      setSelectedDefinition(definition);
    }

    const versionPayload = {
      formDefinitionVersionId: request.formDefinitionVersionId,
      graph: request.graph,
    };
    let version = selectedVersion?.status === "Draft"
      ? await api.updateProcessDefinitionVersion(token, definition.id, selectedVersion.id, versionPayload)
      : await api.createProcessDefinitionVersion(token, definition.id, versionPayload);
    setSelectedVersion(version);

    if (publish) {
      version = await api.publishProcessDefinitionVersion(token, definition.id, version.id);
      setSelectedVersion(version);
    }

    const nextDraft = addLookupLabels(versionToDraft(definition, version), lookups);
    setSelectedDefinition(definition);
    setSelectedVersion(version);
    setDraft(nextDraft);
    setDefinitions(await api.listProcessDefinitions(token));
    setWorkspaceMessage(publish
      ? text("Yayınlanan sürüm salt okunur açıldı.", "The published version was opened as read only.")
      : text(`v${version.versionNumber} taslağı kaydedildi.`, `Draft v${version.versionNumber} was saved.`));
    return { definition, version, draft: nextDraft };
  }

  async function saveWorkflow(request: SaveWorkflowDraftRequest): Promise<WorkflowMutationResult> {
    const result = await persistWorkflow(request, false);
    return { draft: result.draft, message: text("Akış taslağı kaydedildi.", "Workflow draft was saved.") };
  }

  async function publishWorkflow(request: SaveWorkflowDraftRequest): Promise<WorkflowMutationResult> {
    const result = await persistWorkflow(request, true);
    return { draft: result.draft, message: text("Akış sürümü yayınlandı.", "Workflow version was published.") };
  }

  function startNewDefinition() {
    setSelectedDefinition(null);
    setSelectedVersion(null);
    setDraft(createStarterWorkflowDraft(getNextWorkflowName(definitions)));
    setWorkspaceMessage(text("Yeni akış taslağı hazır.", "The new workflow draft is ready."));
    void loadLookups(selectedCommunityId);
  }

  function createDraftFromPublished() {
    if (!selectedDefinition || !selectedVersion) {
      return;
    }
    const cloned = versionToDraft(selectedDefinition, selectedVersion);
    setSelectedVersion(null);
    setDraft({ ...cloned, id: undefined, version: undefined, status: "Draft", publishedAt: null });
    setWorkspaceMessage(text("Yayın sürümünden yeni taslak oluşturuldu.", "A new draft was created from the published version."));
  }

  function importWorkflowDraft(imported: WorkflowDefinitionDraft) {
    setSelectedDefinition(null);
    setSelectedVersion(null);
    setDraft(imported);
    setWorkspaceMessage(text(
      "İçe aktarılan akış yeni taslak olarak açıldı. Ortam bağlantılarını yeniden seçin.",
      "The imported workflow was opened as a new draft. Rebind its environment references.",
    ));
  }

  const editorKey = useMemo(
    () => `${selectedDefinition?.id ?? "new"}:${selectedVersion?.id ?? "draft"}:${draft.status}`,
    [draft.status, selectedDefinition?.id, selectedVersion?.id],
  );

  return (
    <section className="workflow-workspace-view">
      <div className="section-heading workflow-workspace-heading">
        <div>
          <span className="eyebrow">{text("Süreç Tasarımı", "Process Design")}</span>
          <h2>{text("Görsel İş Akışları", "Visual Workflows")}</h2>
        </div>
        <p>{text(
          "Versiyonlu süreç grafiklerini, görev atamalarını ve karar yollarını yönetin.",
          "Manage versioned process graphs, task assignments, and decision paths.",
        )}</p>
      </div>

      <div className="workflow-workspace-toolbar">
        {isSuperAdmin ? (
          <label className="workflow-definition-select workflow-community-select">
            <span>{text("Topluluk", "Community")}</span>
            <select
              disabled={Boolean(selectedDefinition) || status === "loading"}
              onChange={(event) => {
                const communityId = event.target.value;
                setSelectedCommunityId(communityId);
                void loadLookups(communityId);
              }}
              value={selectedCommunityId}
            >
              <option value="">{text("Topluluk seçin", "Select community")}</option>
              {communities.map((community) => (
                <option key={community.id} value={community.id}>{community.name}</option>
              ))}
            </select>
          </label>
        ) : null}
        <label className="workflow-definition-select">
          <span>{text("Akış tanımı", "Workflow definition")}</span>
          <select
            disabled={status === "loading" || definitions.length === 0}
            onChange={(event) => void openDefinition(event.target.value)}
            value={selectedDefinition?.id ?? ""}
          >
            {!selectedDefinition ? <option value="">{text("Yeni akış", "New workflow")}</option> : null}
            {definitions.map((definition) => (
              <option key={definition.id} value={definition.id}>
                {definition.name}{definition.latestVersionNumber ? ` · v${definition.latestVersionNumber}` : ""}
              </option>
            ))}
          </select>
        </label>
        <div className="workflow-workspace-toolbar-actions">
          {selectedVersion?.status === "Published" && canUpdate ? (
            <Button leadingIcon={<CopyPlus size={16} aria-hidden="true" />} onClick={createDraftFromPublished} size="sm" variant="secondary">
              {text("Yeni sürüm", "New version")}
            </Button>
          ) : null}
          <Button
            disabled={!canCreateDefinition}
            leadingIcon={<Plus size={16} aria-hidden="true" />}
            onClick={startNewDefinition}
            size="sm"
            variant="secondary"
          >
            {text("Yeni akış", "New workflow")}
          </Button>
          <Button
            disabled={isRefreshing || status === "loading"}
            isLoading={isRefreshing}
            leadingIcon={<RefreshCw size={16} aria-hidden="true" />}
            onClick={() => void loadWorkspace(true)}
            size="sm"
            variant="secondary"
          >
            {text("Yenile", "Refresh")}
          </Button>
        </div>
        <p className={`workflow-workspace-message workflow-workspace-message-${status}`} aria-live="polite">
          {workspaceMessage}
        </p>
      </div>

      {status === "loading" ? (
        <WorkflowWorkspaceSkeleton />
      ) : status === "error" ? (
        <div className="workflow-workspace-error">
          <p>{workspaceMessage}</p>
          <Button onClick={() => void loadWorkspace(true)} size="sm" variant="secondary">
            {text("Tekrar dene", "Try again")}
          </Button>
        </div>
      ) : (
        <WorkflowEditor
          canPublish={canPublish}
          initialDraft={draft}
          key={editorKey}
          lookups={lookups}
          onImportDraft={canCreateDefinition ? importWorkflowDraft : undefined}
          onPublish={publishWorkflow}
          onSave={saveWorkflow}
          readOnly={!canEditCurrent}
        />
      )}
    </section>
  );
}

function selectEditableVersion(versions: ProcessDefinitionVersion[]) {
  const byNewest = [...versions].sort((left, right) => right.versionNumber - left.versionNumber);
  return byNewest.find((version) => version.status === "Draft")
    ?? byNewest.find((version) => version.status === "Published")
    ?? null;
}

function versionToDraft(definition: ProcessDefinition, version: ProcessDefinitionVersion) {
  return fromApiProcessGraph(version.graph, {
    id: version.id,
    version: version.versionNumber,
    name: definition.name,
    description: definition.description,
    status: version.status === "Published" ? "Published" : "Draft",
    publishedAt: version.publishedAt,
    formDefinitionVersionId: version.formDefinitionVersionId,
  });
}

function createDefinitionDraft(definition: ProcessDefinition) {
  const draft = createStarterWorkflowDraft();
  return { ...draft, name: definition.name, description: definition.description };
}

function addLookupLabels(draft: WorkflowDefinitionDraft, lookups: WorkflowEditorLookups) {
  return resolveLookupLabels(draft, {
    people: Object.fromEntries(lookups.people.map((option) => [option.id, option.label])),
    teams: Object.fromEntries(lookups.teams.map((option) => [option.id, option.label])),
    communityRoles: Object.fromEntries(lookups.communityRoles.map((option) => [option.id, option.label])),
    formVersions: Object.fromEntries(lookups.formVersions.map((option) => [option.id, {
      name: option.label,
      version: option.version,
    }])),
  });
}

async function settle<T>(request: Promise<T>, fallback: T) {
  try {
    return await request;
  } catch {
    return fallback;
  }
}

function conditionValueType(fieldType: string): "String" | "Number" | "Boolean" {
  if (fieldType === "Number") return "Number";
  if (fieldType === "Checkbox") return "Boolean";
  return "String";
}

function toErrorMessage(error: unknown, language: "tr" | "en", fallback: string) {
  if (error instanceof ApiError) {
    return localizeApiError(error, language, fallback);
  }
  return error instanceof Error && error.message ? error.message : fallback;
}

function WorkflowWorkspaceSkeleton() {
  const language = useSessionStore((state) => state.language);
  return (
    <div
      className="workflow-workspace-skeleton"
      aria-label={workflowText(language, "Akış editörü yükleniyor", "Loading workflow editor")}
    >
      <span />
      <span />
      <span />
    </div>
  );
}
