import type {
  WorkflowAssignmentType,
  WorkflowConditionOperator,
  WorkflowConditionValueType,
  WorkflowFormMode,
  WorkflowNodeKind,
  WorkflowTaskAction,
  WorkflowTaskPriority,
} from "@/features/workflows/contracts";
import type { Language } from "@/lib/types";

export const workflowNodeLabels: Record<WorkflowNodeKind, string> = {
  start: "Başlangıç",
  userTask: "Kullanıcı görevi",
  exclusiveGateway: "Karar",
  completedEnd: "Tamamlanan sonuç",
  rejectedEnd: "Reddedilen sonuç",
  teamSwimlane: "Takım kulvarı",
};

export const workflowActionLabels: Record<WorkflowTaskAction, string> = {
  Approve: "Onayla",
  Reject: "Reddet",
  Escalate: "Yükselt",
  SendBack: "Geri gönder",
  Complete: "Tamamla",
};

export const workflowPriorityLabels: Record<WorkflowTaskPriority, string> = {
  Low: "Düşük",
  Normal: "Normal",
  High: "Yüksek",
  Critical: "Kritik",
};

export const workflowAssignmentLabels: Record<WorkflowAssignmentType, string> = {
  processStarter: "Süreci başlatan",
  person: "Belirli kullanıcı",
  team: "Takım havuzu",
  communityRole: "Topluluk rolü",
  teamAndRole: "Takım ve rol",
};

export const workflowConditionOperatorLabels: Record<WorkflowConditionOperator, string> = {
  Equals: "Eşittir",
  NotEquals: "Eşit değildir",
  Contains: "İçerir",
  GreaterThan: "Büyüktür",
  GreaterThanOrEquals: "Büyük veya eşittir",
  LessThan: "Küçüktür",
  LessThanOrEquals: "Küçük veya eşittir",
  IsEmpty: "Boştur",
  IsNotEmpty: "Boş değildir",
};

export const workflowConditionValueTypeLabels: Record<WorkflowConditionValueType, string> = {
  String: "Metin",
  Number: "Sayı",
  Boolean: "Doğru / yanlış",
};

export const workflowFormModeLabels: Record<WorkflowFormMode, string> = {
  Required: "Zorunlu",
  Optional: "İsteğe bağlı",
  ReadOnly: "Salt okunur",
};

const workflowNodeLabelsEn: Record<WorkflowNodeKind, string> = {
  start: "Start",
  userTask: "User task",
  exclusiveGateway: "Decision",
  completedEnd: "Completed result",
  rejectedEnd: "Rejected result",
  teamSwimlane: "Team swimlane",
};

const workflowActionLabelsEn: Record<WorkflowTaskAction, string> = {
  Approve: "Approve",
  Reject: "Reject",
  Escalate: "Escalate",
  SendBack: "Send back",
  Complete: "Complete",
};

const workflowPriorityLabelsEn: Record<WorkflowTaskPriority, string> = {
  Low: "Low",
  Normal: "Normal",
  High: "High",
  Critical: "Critical",
};

const workflowAssignmentLabelsEn: Record<WorkflowAssignmentType, string> = {
  processStarter: "Process starter",
  person: "Specific user",
  team: "Team pool",
  communityRole: "Community role",
  teamAndRole: "Team and role",
};

const workflowConditionOperatorLabelsEn: Record<WorkflowConditionOperator, string> = {
  Equals: "Equals",
  NotEquals: "Does not equal",
  Contains: "Contains",
  GreaterThan: "Greater than",
  GreaterThanOrEquals: "Greater than or equal",
  LessThan: "Less than",
  LessThanOrEquals: "Less than or equal",
  IsEmpty: "Is empty",
  IsNotEmpty: "Is not empty",
};

const workflowConditionValueTypeLabelsEn: Record<WorkflowConditionValueType, string> = {
  String: "Text",
  Number: "Number",
  Boolean: "True / false",
};

const workflowFormModeLabelsEn: Record<WorkflowFormMode, string> = {
  Required: "Required",
  Optional: "Optional",
  ReadOnly: "Read only",
};

export function getWorkflowNodeLabels(language: Language) {
  return language === "tr" ? workflowNodeLabels : workflowNodeLabelsEn;
}

export function getWorkflowActionLabels(language: Language) {
  return language === "tr" ? workflowActionLabels : workflowActionLabelsEn;
}

export function getWorkflowPriorityLabels(language: Language) {
  return language === "tr" ? workflowPriorityLabels : workflowPriorityLabelsEn;
}

export function getWorkflowAssignmentLabels(language: Language) {
  return language === "tr" ? workflowAssignmentLabels : workflowAssignmentLabelsEn;
}

export function getWorkflowConditionOperatorLabels(language: Language) {
  return language === "tr" ? workflowConditionOperatorLabels : workflowConditionOperatorLabelsEn;
}

export function getWorkflowConditionValueTypeLabels(language: Language) {
  return language === "tr" ? workflowConditionValueTypeLabels : workflowConditionValueTypeLabelsEn;
}

export function getWorkflowFormModeLabels(language: Language) {
  return language === "tr" ? workflowFormModeLabels : workflowFormModeLabelsEn;
}
