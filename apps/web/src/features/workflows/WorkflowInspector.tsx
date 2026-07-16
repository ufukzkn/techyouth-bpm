import {
  ArrowRight,
  CircleCheck,
  CircleX,
  ClipboardCheck,
  GitFork,
  Play,
  Rows3,
  Settings2,
  Trash2,
} from "lucide-react";
import type { ReactNode } from "react";
import type {
  WorkflowAssignment,
  WorkflowAssignmentType,
  WorkflowCondition,
  WorkflowConditionOperator,
  WorkflowConditionValueType,
  WorkflowDefinitionDraft,
  WorkflowEditorLookups,
  WorkflowFormBinding,
  WorkflowLookupOption,
  WorkflowNode,
  WorkflowTaskAction,
  WorkflowTaskPriority,
  WorkflowTransition,
  WorkflowValidationIssue,
} from "@/features/workflows/contracts";
import { IconButton } from "@/features/ui/IconButton";
import { createEmptyAssignment } from "@/features/workflows/workflowDraft";
import {
  workflowActionLabels,
  workflowAssignmentLabels,
  workflowConditionOperatorLabels,
  workflowConditionValueTypeLabels,
  workflowNodeLabels,
  workflowPriorityLabels,
} from "@/features/workflows/workflowLabels";
import { useWorkflowDraftStore } from "@/features/workflows/workflowDraftStore";
import { WorkflowValidationPanel } from "@/features/workflows/WorkflowValidationPanel";

type WorkflowInspectorProps = {
  issues: WorkflowValidationIssue[];
  lookups: WorkflowEditorLookups;
  readOnly: boolean;
};

const assignmentTypes: WorkflowAssignmentType[] = [
  "processStarter",
  "person",
  "team",
  "communityRole",
  "teamAndRole",
];
const taskActions: WorkflowTaskAction[] = ["Approve", "Reject", "Complete", "Escalate", "SendBack"];
const actionPresets: Record<string, WorkflowTaskAction[]> = {
  approval: ["Approve", "Reject", "SendBack"],
  operation: ["Complete", "SendBack"],
  review: ["Approve", "Reject"],
};
const priorities: WorkflowTaskPriority[] = ["Low", "Normal", "High", "Critical"];
const conditionOperators = Object.keys(workflowConditionOperatorLabels) as WorkflowConditionOperator[];

export function WorkflowInspector({ issues, lookups, readOnly }: WorkflowInspectorProps) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const selectedNodeId = useWorkflowDraftStore((state) => state.selectedNodeId);
  const selectedEdgeId = useWorkflowDraftStore((state) => state.selectedEdgeId);
  const selectedNode = draft.nodes.find((node) => node.id === selectedNodeId);
  const selectedEdge = draft.edges.find((edge) => edge.id === selectedEdgeId);
  const deleteSelection = useWorkflowDraftStore((state) => state.deleteSelection);

  return (
    <aside className="workflow-inspector" aria-label="Özellik denetçisi">
      <div className="workflow-inspector-scroll">
        {selectedNode ? (
          <SelectedNodeInspector node={selectedNode} lookups={lookups} readOnly={readOnly} />
        ) : selectedEdge ? (
          <SelectedEdgeInspector edge={selectedEdge} lookups={lookups} readOnly={readOnly} />
        ) : (
          <WorkflowMetadataInspector readOnly={readOnly} />
        )}
      </div>

      {(selectedNode || selectedEdge) && !readOnly ? (
        <div className="workflow-inspector-delete">
          <IconButton label="Seçimi sil" onClick={deleteSelection} tone="danger">
            <Trash2 size={16} aria-hidden="true" />
          </IconButton>
          <span>{selectedNode ? "Düğümü sil" : "Bağlantıyı sil"}</span>
        </div>
      ) : null}

      <WorkflowValidationPanel issues={issues} />
    </aside>
  );
}

function WorkflowMetadataInspector({ readOnly }: { readOnly: boolean }) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const setMetadata = useWorkflowDraftStore((state) => state.setMetadata);
  const flowNodeCount = draft.nodes.filter((node) => node.type !== "teamSwimlane").length;

  return (
    <>
      <InspectorHeading icon={<Settings2 size={17} />} kicker="Akış" title="Genel özellikler" />
      <div className="workflow-inspector-fields">
        <InspectorField label="Akış adı">
          <input
            disabled={readOnly}
            maxLength={140}
            onChange={(event) => setMetadata({ name: event.target.value })}
            value={draft.name}
          />
        </InspectorField>
        <InspectorField label="Açıklama">
          <textarea
            disabled={readOnly}
            maxLength={500}
            onChange={(event) => setMetadata({ description: event.target.value })}
            rows={4}
            value={draft.description}
          />
        </InspectorField>
        <dl className="workflow-inspector-facts">
          <div><dt>Durum</dt><dd>{draft.status === "Published" ? "Yayında" : "Taslak"}</dd></div>
          <div><dt>Sürüm</dt><dd>{draft.version ?? "Yeni"}</dd></div>
          <div><dt>Düğüm</dt><dd>{flowNodeCount}</dd></div>
          <div><dt>Bağlantı</dt><dd>{draft.edges.length}</dd></div>
        </dl>
      </div>
    </>
  );
}

function SelectedNodeInspector({
  node,
  lookups,
  readOnly,
}: {
  node: WorkflowNode;
  lookups: WorkflowEditorLookups;
  readOnly: boolean;
}) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const updateNodeData = useWorkflowDraftStore((state) => state.updateNodeData);

  function updateCommon(patch: { label?: string; description?: string }) {
    updateNodeData(node.id, (data) => ({ ...data, ...patch }));
  }

  return (
    <>
      <InspectorHeading icon={nodeIcon(node.type)} kicker={workflowNodeLabels[node.type]} title={node.data.label || "Adsız düğüm"} />
      <div className="workflow-inspector-fields">
        <InspectorField label={node.type === "teamSwimlane" ? "Kulvar adı" : "Başlık"}>
          <input
            disabled={readOnly}
            maxLength={140}
            onChange={(event) => updateCommon({ label: event.target.value })}
            value={node.data.label}
          />
        </InspectorField>
        <InspectorField label="Açıklama">
          <textarea
            disabled={readOnly}
            maxLength={500}
            onChange={(event) => updateCommon({ description: event.target.value })}
            rows={3}
            value={node.data.description}
          />
        </InspectorField>

        {node.type === "start" ? (
          <FormBindingFields
            binding={node.data.formBinding}
            lookups={lookups}
            onChange={(formBinding) => updateNodeData(node.id, (data) => data.kind === "start" ? { ...data, formBinding } : data)}
            readOnly={readOnly}
          />
        ) : null}

        {node.type === "userTask" ? (
          <UserTaskFields node={node} lookups={lookups} readOnly={readOnly} />
        ) : null}

        {node.type === "exclusiveGateway" ? <GatewaySummary node={node} /> : null}

        {node.type === "teamSwimlane" ? (
          <InspectorSection title="Takım">
            <LookupSelect
              disabled={readOnly}
              emptyLabel="Takım seçin"
              onChange={(teamId, teamName) => {
                const previousTeamId = node.data.teamId;
                updateNodeData(
                  node.id,
                  (data) => data.kind === "teamSwimlane" ? { ...data, teamId, teamName } : data,
                );
                draft.nodes
                  .filter((candidate) => candidate.parentId === node.id && candidate.type === "userTask")
                  .forEach((candidate) => updateNodeData(candidate.id, (data) => {
                    if (data.kind !== "userTask" || data.assignment.type !== "team") return data;
                    if (data.assignment.teamId && data.assignment.teamId !== previousTeamId) return data;
                    return { ...data, assignment: { type: "team", teamId, teamName } };
                  }));
              }}
              options={lookups.teams}
              value={node.data.teamId}
              valueLabel={node.data.teamName}
            />
          </InspectorSection>
        ) : null}
      </div>
    </>
  );
}

function UserTaskFields({
  node,
  lookups,
  readOnly,
}: {
  node: Extract<WorkflowNode, { type: "userTask" }>;
  lookups: WorkflowEditorLookups;
  readOnly: boolean;
}) {
  const updateNodeData = useWorkflowDraftStore((state) => state.updateNodeData);

  function updateTask(patch: Partial<typeof node.data>) {
    updateNodeData(node.id, (data) => data.kind === "userTask" ? { ...data, ...patch } : data);
  }

  function toggleAction(action: WorkflowTaskAction) {
    const actions = node.data.actions.includes(action)
      ? node.data.actions.filter((candidate) => candidate !== action)
      : [...node.data.actions, action];
    updateTask({ actions });
  }

  const slaUnit = node.data.slaUnit
    ?? (node.data.slaDurationMinutes && node.data.slaDurationMinutes % 1_440 === 0 ? "days" : "hours");
  const slaFactor = slaUnit === "days" ? 1_440 : 60;
  const slaValue = node.data.slaDurationMinutes == null
    ? ""
    : Number((node.data.slaDurationMinutes / slaFactor).toFixed(2));

  return (
    <>
      <InspectorSection title="Atama">
        <InspectorField label="Atama türü">
          <select
            disabled={readOnly}
            onChange={(event) => updateTask({ assignment: createEmptyAssignment(event.target.value as WorkflowAssignmentType) })}
            value={node.data.assignment.type}
          >
            {assignmentTypes.map((type) => <option key={type} value={type}>{workflowAssignmentLabels[type]}</option>)}
          </select>
        </InspectorField>
        <AssignmentTargetFields
          assignment={node.data.assignment}
          lookups={lookups}
          onChange={(assignment) => updateTask({ assignment })}
          readOnly={readOnly}
        />
      </InspectorSection>

      <InspectorSection title="Öncelik">
        <InspectorField label="Görev önceliği">
          <select
            disabled={readOnly}
            onChange={(event) => updateTask({ priority: event.target.value as WorkflowTaskPriority })}
            value={node.data.priority}
          >
            {priorities.map((priority) => <option key={priority} value={priority}>{workflowPriorityLabels[priority]}</option>)}
          </select>
        </InspectorField>
        <div className="workflow-sla-fields">
          <InspectorField label="SLA süresi">
            <input
              disabled={readOnly}
              inputMode="decimal"
              min="0.02"
              onChange={(event) => {
                const rawValue = event.target.value;
                if (!rawValue) {
                  updateTask({ slaDurationMinutes: null });
                  return;
                }
                const value = Number(rawValue);
                if (Number.isFinite(value)) {
                  updateTask({
                    slaDurationMinutes: Math.min(525_600, Math.max(1, Math.round(value * slaFactor))),
                  });
                }
              }}
              placeholder="Sınırsız"
              step="0.25"
              type="number"
              value={slaValue}
            />
          </InspectorField>
          <InspectorField label="Birim">
            <select
              disabled={readOnly || node.data.slaDurationMinutes == null}
              onChange={(event) => {
                const nextUnit = event.target.value as "hours" | "days";
                const currentValue = typeof slaValue === "number" ? slaValue : 1;
                const nextFactor = nextUnit === "days" ? 1_440 : 60;
                updateTask({
                  slaUnit: nextUnit,
                  slaDurationMinutes: Math.min(525_600, Math.max(1, Math.round(currentValue * nextFactor))),
                });
              }}
              value={slaUnit}
            >
              <option value="hours">Saat</option>
              <option value="days">Gün</option>
            </select>
          </InspectorField>
        </div>
        <p className="workflow-inspector-hint">Boş bırakıldığında görev için son tarih hesaplanmaz.</p>
      </InspectorSection>

      <InspectorSection title="İşlemler">
        <InspectorField label="İşlem şablonu">
          <select
            disabled={readOnly}
            onChange={(event) => {
              const actions = actionPresets[event.target.value];
              if (actions) updateTask({ actions });
            }}
            value={resolveActionPreset(node.data.actions)}
          >
            <option value="custom">Özel</option>
            <option value="approval">Onay</option>
            <option value="operation">Operasyon</option>
            <option value="review">İnceleme</option>
          </select>
        </InspectorField>
        <div className="workflow-check-grid">
          {taskActions.map((action) => (
            <label className="workflow-check-option" key={action}>
              <input
                checked={node.data.actions.includes(action)}
                disabled={readOnly}
                onChange={() => toggleAction(action)}
                type="checkbox"
              />
              <span>{workflowActionLabels[action]}</span>
            </label>
          ))}
        </div>
      </InspectorSection>

      <FormBindingFields
        binding={node.data.formBinding}
        lookups={lookups}
        onChange={(formBinding) => updateTask({ formBinding })}
        readOnly={readOnly}
      />
    </>
  );
}

function resolveActionPreset(actions: WorkflowTaskAction[]) {
  const normalized = [...actions].sort().join("|");
  return Object.entries(actionPresets).find(([, preset]) => [...preset].sort().join("|") === normalized)?.[0] ?? "custom";
}

function AssignmentTargetFields({
  assignment,
  lookups,
  onChange,
  readOnly,
}: {
  assignment: WorkflowAssignment;
  lookups: WorkflowEditorLookups;
  onChange: (assignment: WorkflowAssignment) => void;
  readOnly: boolean;
}) {
  switch (assignment.type) {
    case "processStarter":
      return null;
    case "person":
      return (
        <InspectorField label="Kullanıcı">
          <LookupSelect
            disabled={readOnly}
            emptyLabel="Kullanıcı seçin"
            onChange={(personId, personName) => onChange({ ...assignment, personId, personName })}
            options={lookups.people}
            value={assignment.personId}
            valueLabel={assignment.personName}
          />
        </InspectorField>
      );
    case "team":
      return (
        <InspectorField label="Takım">
          <LookupSelect
            disabled={readOnly}
            emptyLabel="Takım seçin"
            onChange={(teamId, teamName) => onChange({ ...assignment, teamId, teamName })}
            options={lookups.teams}
            value={assignment.teamId}
            valueLabel={assignment.teamName}
          />
        </InspectorField>
      );
    case "communityRole":
      return (
        <InspectorField label="Topluluk rolü">
          <LookupSelect
            disabled={readOnly}
            emptyLabel="Rol seçin"
            onChange={(communityRoleId, communityRoleName) => onChange({
              ...assignment,
              communityRoleId,
              communityRoleName,
            })}
            options={lookups.communityRoles}
            value={assignment.communityRoleId}
            valueLabel={assignment.communityRoleName}
          />
        </InspectorField>
      );
    case "teamAndRole":
      return (
        <>
          <InspectorField label="Takım">
            <LookupSelect
              disabled={readOnly}
              emptyLabel="Takım seçin"
              onChange={(teamId, teamName) => onChange({ ...assignment, teamId, teamName })}
              options={lookups.teams}
              value={assignment.teamId}
              valueLabel={assignment.teamName}
            />
          </InspectorField>
          <InspectorField label="Topluluk rolü">
            <LookupSelect
              disabled={readOnly}
              emptyLabel="Rol seçin"
              onChange={(communityRoleId, communityRoleName) => onChange({
                ...assignment,
                communityRoleId,
                communityRoleName,
              })}
              options={lookups.communityRoles}
              value={assignment.communityRoleId}
              valueLabel={assignment.communityRoleName}
            />
          </InspectorField>
        </>
      );
  }
}

function FormBindingFields({
  binding,
  lookups,
  onChange,
  readOnly,
}: {
  binding: WorkflowFormBinding | null;
  lookups: WorkflowEditorLookups;
  onChange: (binding: WorkflowFormBinding | null) => void;
  readOnly: boolean;
}) {
  function toggle(enabled: boolean) {
    const first = lookups.formVersions[0];
    onChange(enabled ? {
      formVersionId: first?.id ?? "",
      formName: first?.label ?? "",
      version: first?.version ?? null,
      mode: "Required",
    } : null);
  }

  return (
    <InspectorSection title="Form bağlantısı">
      <label className="workflow-switch-row">
        <span>Form kullan</span>
        <input
          checked={Boolean(binding)}
          disabled={readOnly}
          onChange={(event) => toggle(event.target.checked)}
          type="checkbox"
        />
      </label>
      {binding ? (
        <InspectorField label="Form sürümü">
          <select
            disabled={readOnly}
            onChange={(event) => {
              const option = lookups.formVersions.find((candidate) => candidate.id === event.target.value);
              onChange({
                ...binding,
                formVersionId: event.target.value,
                formName: option?.label ?? "",
                version: option?.version ?? null,
              });
            }}
            value={binding.formVersionId}
          >
            <option value="">Form sürümü seçin</option>
            {binding.formVersionId && !lookups.formVersions.some((option) => option.id === binding.formVersionId) ? (
              <option value={binding.formVersionId}>{binding.formName || binding.formVersionId}</option>
            ) : null}
            {lookups.formVersions.map((option) => (
              <option key={option.id} value={option.id}>{option.label} · v{option.version}</option>
            ))}
          </select>
        </InspectorField>
      ) : null}
    </InspectorSection>
  );
}

function GatewaySummary({ node }: { node: Extract<WorkflowNode, { type: "exclusiveGateway" }> }) {
  const allEdges = useWorkflowDraftStore((state) => state.draft.edges);
  const edges = allEdges.filter((edge) => edge.source === node.id);
  return (
    <InspectorSection title="Çıkışlar">
      <div className="workflow-route-list">
        {edges.map((edge, index) => (
          <div key={edge.id}>
            <span>{index + 1}</span>
            <strong>{edge.data?.label || edge.data?.condition?.fieldKey || (edge.data?.isDefault ? "Varsayılan" : "Koşulsuz")}</strong>
          </div>
        ))}
        {edges.length === 0 ? <p>Henüz çıkış yok.</p> : null}
      </div>
    </InspectorSection>
  );
}

function SelectedEdgeInspector({
  edge,
  lookups,
  readOnly,
}: {
  edge: WorkflowTransition;
  lookups: WorkflowEditorLookups;
  readOnly: boolean;
}) {
  const draft = useWorkflowDraftStore((state) => state.draft);
  const updateEdgeData = useWorkflowDraftStore((state) => state.updateEdgeData);
  const source = draft.nodes.find((node) => node.id === edge.source);
  const target = draft.nodes.find((node) => node.id === edge.target);
  const isGatewayEdge = source?.type === "exclusiveGateway";
  const isTaskEdge = source?.type === "userTask";
  const conditionFields = buildConditionFieldOptions(draft, lookups, edge.source);

  function updateCondition(patch: Partial<WorkflowCondition>) {
    const condition: WorkflowCondition = edge.data?.condition ?? {
      fieldKey: "",
      operator: "Equals",
      valueType: "String",
      value: "",
    };
    updateEdgeData(edge.id, { condition: { ...condition, ...patch } });
  }

  return (
    <>
      <InspectorHeading icon={<ArrowRight size={17} />} kicker="Bağlantı" title={edge.data?.label || "Geçiş"} />
      <div className="workflow-inspector-fields">
        <div className="workflow-edge-endpoints">
          <span>{source?.data.label || edge.source}</span>
          <ArrowRight size={14} aria-hidden="true" />
          <span>{target?.data.label || edge.target}</span>
        </div>
        <InspectorField label="Etiket">
          <input
            disabled={readOnly}
            maxLength={100}
            onChange={(event) => updateEdgeData(edge.id, { label: event.target.value })}
            value={edge.data?.label ?? ""}
          />
        </InspectorField>

        {isTaskEdge ? (
          <InspectorSection title="Görev işlemi">
            <InspectorField label="İşlem">
              <select
                disabled={readOnly}
                onChange={(event) => updateEdgeData(edge.id, {
                  action: event.target.value ? event.target.value as WorkflowTaskAction : null,
                })}
                value={edge.data?.action ?? ""}
              >
                <option value="">İşlem seçin</option>
                {source.data.actions.map((action) => (
                  <option key={action} value={action}>{workflowActionLabels[action]}</option>
                ))}
              </select>
            </InspectorField>
          </InspectorSection>
        ) : null}

        {isGatewayEdge ? (
          <InspectorSection title="Karar koşulu">
            <label className="workflow-switch-row">
              <span>Varsayılan çıkış</span>
              <input
                checked={edge.data?.isDefault ?? false}
                disabled={readOnly}
                onChange={(event) => updateEdgeData(edge.id, {
                  isDefault: event.target.checked,
                  condition: event.target.checked ? null : edge.data?.condition,
                })}
                type="checkbox"
              />
            </label>
            {!edge.data?.isDefault ? (
              <>
                <InspectorField label="Form yolu">
                  <select
                    disabled={readOnly}
                    onChange={(event) => {
                      const option = conditionFields.find((candidate) => candidate.path === event.target.value);
                      const nextType = option?.valueType ?? "String";
                      const validOperators = getConditionOperators(nextType);
                      updateCondition({
                        fieldKey: event.target.value,
                        valueType: nextType,
                        operator: validOperators.includes(edge.data?.condition?.operator ?? "Equals")
                          ? edge.data?.condition?.operator ?? "Equals"
                          : validOperators[0],
                        value: "",
                      });
                    }}
                    value={edge.data?.condition?.fieldKey ?? ""}
                  >
                    <option value="">Form alanı seçin</option>
                    {edge.data?.condition?.fieldKey
                      && !conditionFields.some((option) => option.path === edge.data?.condition?.fieldKey) ? (
                        <option value={edge.data.condition.fieldKey}>{edge.data.condition.fieldKey}</option>
                      ) : null}
                    {conditionFields.map((option) => (
                      <option key={option.path} value={option.path}>{option.label}</option>
                    ))}
                  </select>
                </InspectorField>
                <InspectorField label="Operatör">
                  <select
                    disabled={readOnly}
                    onChange={(event) => updateCondition({ operator: event.target.value as WorkflowConditionOperator })}
                    value={edge.data?.condition?.operator ?? "Equals"}
                  >
                    {getConditionOperators(edge.data?.condition?.valueType ?? "String").map((operator) => (
                      <option key={operator} value={operator}>{workflowConditionOperatorLabels[operator]}</option>
                    ))}
                  </select>
                </InspectorField>
                {!isValuelessOperator(edge.data?.condition?.operator) ? (
                  <>
                    <InspectorField label="Değer türü">
                      <span className="workflow-inspector-readonly-value">
                        {workflowConditionValueTypeLabels[edge.data?.condition?.valueType ?? "String"]}
                      </span>
                    </InspectorField>
                    <ConditionValueField
                      condition={edge.data?.condition}
                      onChange={(value) => updateCondition({ value })}
                      readOnly={readOnly}
                    />
                  </>
                ) : null}
              </>
            ) : null}
          </InspectorSection>
        ) : null}
      </div>
    </>
  );
}

function buildConditionFieldOptions(
  draft: WorkflowDefinitionDraft,
  lookups: WorkflowEditorLookups,
  gatewayNodeId: string,
) {
  const versions = new Map(lookups.formVersions.map((version) => [version.id, version]));
  return draft.nodes.flatMap((node) => {
    if (node.type === "start" && node.data.formBinding) {
      const version = versions.get(node.data.formBinding.formVersionId);
      return (version?.fields ?? []).map((field) => ({
        path: `start.${field.key}`,
        label: `Başlangıç / ${field.label}`,
        valueType: field.valueType,
      }));
    }
    if (node.type === "userTask"
      && node.data.formBinding
      && hasForwardPath(node.id, gatewayNodeId, draft.edges)) {
      const version = versions.get(node.data.formBinding.formVersionId);
      return (version?.fields ?? []).map((field) => ({
        path: `steps.${node.id}.${field.key}`,
        label: `${node.data.label} / ${field.label}`,
        valueType: field.valueType,
      }));
    }
    return [];
  });
}

function hasForwardPath(source: string, target: string, edges: WorkflowTransition[]) {
  const visited = new Set<string>();
  const pending = [source];
  while (pending.length > 0) {
    const current = pending.shift();
    if (!current || visited.has(current)) continue;
    if (current === target) return true;
    visited.add(current);
    edges
      .filter((edge) => edge.source === current && edge.data?.action !== "SendBack")
      .forEach((edge) => pending.push(edge.target));
  }
  return false;
}

function getConditionOperators(valueType: WorkflowConditionValueType) {
  return conditionOperators.filter((operator) => {
    if (operator === "GreaterThan"
      || operator === "GreaterThanOrEquals"
      || operator === "LessThan"
      || operator === "LessThanOrEquals") {
      return valueType === "Number";
    }
    if (operator === "Contains") {
      return valueType === "String";
    }
    return true;
  });
}

function ConditionValueField({
  condition,
  onChange,
  readOnly,
}: {
  condition: WorkflowCondition | null | undefined;
  onChange: (value: string) => void;
  readOnly: boolean;
}) {
  if (condition?.valueType === "Boolean") {
    return (
      <InspectorField label="Değer">
        <select disabled={readOnly} onChange={(event) => onChange(event.target.value)} value={condition.value || "true"}>
          <option value="true">Doğru</option>
          <option value="false">Yanlış</option>
        </select>
      </InspectorField>
    );
  }
  return (
    <InspectorField label="Değer">
      <input
        disabled={readOnly}
        inputMode={condition?.valueType === "Number" ? "decimal" : undefined}
        onChange={(event) => onChange(event.target.value)}
        value={condition?.value ?? ""}
      />
    </InspectorField>
  );
}

function LookupSelect({
  disabled,
  emptyLabel,
  onChange,
  options,
  value,
  valueLabel,
}: {
  disabled: boolean;
  emptyLabel: string;
  onChange: (id: string, label: string) => void;
  options: WorkflowLookupOption[];
  value: string;
  valueLabel: string;
}) {
  return (
    <select
      disabled={disabled}
      onChange={(event) => {
        const option = options.find((candidate) => candidate.id === event.target.value);
        onChange(event.target.value, option?.label ?? "");
      }}
      value={value}
    >
      <option value="">{emptyLabel}</option>
      {value && !options.some((option) => option.id === value) ? <option value={value}>{valueLabel || value}</option> : null}
      {options.map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
    </select>
  );
}

function InspectorHeading({ icon, kicker, title }: { icon: ReactNode; kicker: string; title: string }) {
  return (
    <div className="workflow-inspector-heading">
      <span className="workflow-inspector-heading-icon" aria-hidden="true">{icon}</span>
      <span>
        <small>{kicker}</small>
        <strong>{title}</strong>
      </span>
    </div>
  );
}

function InspectorSection({ children, title }: { children: ReactNode; title: string }) {
  return (
    <section className="workflow-inspector-section">
      <h3>{title}</h3>
      <div>{children}</div>
    </section>
  );
}

function InspectorField({ children, label }: { children: ReactNode; label: string }) {
  return (
    <label className="workflow-inspector-field">
      <span>{label}</span>
      {children}
    </label>
  );
}

function nodeIcon(type: WorkflowNode["type"]) {
  switch (type) {
    case "start":
      return <Play size={17} />;
    case "userTask":
      return <ClipboardCheck size={17} />;
    case "exclusiveGateway":
      return <GitFork size={17} />;
    case "completedEnd":
      return <CircleCheck size={17} />;
    case "rejectedEnd":
      return <CircleX size={17} />;
    case "teamSwimlane":
      return <Rows3 size={17} />;
  }
}

function isValuelessOperator(operator: WorkflowConditionOperator | undefined) {
  return operator === "IsEmpty" || operator === "IsNotEmpty";
}
