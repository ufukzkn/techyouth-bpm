import type {
  WorkflowAssignmentType,
  WorkflowConditionOperator,
  WorkflowConditionValueType,
  WorkflowFormMode,
  WorkflowNodeKind,
  WorkflowTaskAction,
  WorkflowTaskPriority,
} from "@/features/workflows/contracts";

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
