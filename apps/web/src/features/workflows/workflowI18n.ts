import type { WorkflowValidationIssue } from "@/features/workflows/contracts";
import type { Language } from "@/lib/types";

export function workflowText(language: Language, tr: string, en: string) {
  return language === "tr" ? tr : en;
}

const validationMessagesEn: Record<string, string> = {
  "workflow.node.keys.duplicate": "Node keys must be unique.",
  "workflow.name.required": "Workflow name is required.",
  "workflow.end.required": "The workflow must contain at least one result node.",
  "transition.endpoint.missing": "The source or target node of the connection could not be found.",
  "transition.swimlane.invalid": "Team swimlanes cannot be included in workflow connections.",
  "transition.self.invalid": "A node cannot connect to itself.",
  "node.key.required": "A persistent key is required for the node.",
  "node.label.required": "Node name is required.",
  "node.position.invalid": "Node position must contain valid numbers.",
  "node.size.invalid": "Node dimensions must be positive numbers.",
  "node.parent.invalid": "The node parent must be a team swimlane.",
  "swimlane.team.required": "A team must be selected for the swimlane.",
  "start.form.required": "A form version must be selected for the start node.",
  "start.incoming.invalid": "The start node cannot have an incoming connection.",
  "start.outgoing.count": "The start node must have exactly one outgoing connection.",
  "node.incoming.required": "The node must have at least one incoming connection.",
  "end.outgoing.invalid": "A result node cannot have an outgoing connection.",
  "node.outgoing.required": "The node must have at least one outgoing connection.",
  "task.assignment.required": "Task assignment must be completed.",
  "task.teamLead.assignment": "The team-lead rule can only be used with team or team-and-role assignments.",
  "task.actions.required": "At least one action must be selected for the task.",
  "task.form.required": "A form version must be selected for the form binding.",
  "task.sla.range": "Task SLA must be between 1 minute and 365 days.",
  "gateway.outgoing.minimum": "The decision node must have at least two outgoing connections.",
  "gateway.default.count": "The decision node must have exactly one default connection.",
  "gateway.default.condition.invalid": "A default decision connection cannot contain a condition.",
  "gateway.condition.required": "A non-default decision connection must have a complete condition.",
  "transition.action.unavailable": "The connection action is not available on the source task.",
  "transition.action.required": "An action must be selected for a user-task connection.",
  "transition.sendback.target": "Send back must target a user task.",
  "node.unreachable": "The node is not reachable from the start node.",
  "workflow.forward.cycle": "Automatic workflow connections cannot form a cycle; use Send back to return to an earlier task.",
  "transition.sendback.order": "Send back can only target a user task that appears earlier in the forward workflow.",
  "gateway.condition.future-step": "A decision condition can only use task forms completed before this step.",
  "gateway.condition.field.missing": "The condition uses a field that does not exist in the bound form. Select an available form field.",
  "gateway.condition.type.mismatch": "The condition type does not match the bound form field. Select the field again.",
};

export function localizeWorkflowValidationIssue(issue: WorkflowValidationIssue, language: Language) {
  if (language === "tr") {
    return issue.message;
  }
  if (issue.code === "workflow.start.count") {
    return issue.message.includes("yalnızca")
      ? "The workflow can contain only one start node."
      : "The workflow must contain one start node.";
  }
  if (issue.code === "task.action.route.required" || issue.code === "task.action.route.duplicate") {
    const action = issue.message.split(" ")[0] || "Task";
    return issue.code.endsWith("required")
      ? `${action} must have an outgoing connection.`
      : `${action} can be used by only one outgoing connection.`;
  }
  return validationMessagesEn[issue.code] ?? issue.message;
}
