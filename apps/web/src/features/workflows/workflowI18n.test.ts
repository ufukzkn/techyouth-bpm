import { describe, expect, it } from "vitest";
import type { WorkflowValidationIssue } from "@/features/workflows/contracts";
import {
  getWorkflowActionLabels,
  getWorkflowAssignmentLabels,
  getWorkflowNodeLabels,
} from "@/features/workflows/workflowLabels";
import { localizeWorkflowValidationIssue } from "@/features/workflows/workflowI18n";

describe("workflow i18n", () => {
  it("provides Turkish and English workflow labels from stable keys", () => {
    expect(getWorkflowNodeLabels("tr").userTask).toBe("Kullanıcı görevi");
    expect(getWorkflowNodeLabels("en").userTask).toBe("User task");
    expect(getWorkflowActionLabels("en").SendBack).toBe("Send back");
    expect(getWorkflowAssignmentLabels("en").teamAndRole).toBe("Team and role");
  });

  it("localizes validation messages without changing their stable codes", () => {
    const issue: WorkflowValidationIssue = {
      code: "task.teamLead.assignment",
      severity: "error",
      scope: "node",
      entityId: "finance-approval",
      message: "Takım sorumlusu kuralı yalnız takım veya takım + rol atamalarında kullanılabilir.",
    };

    expect(localizeWorkflowValidationIssue(issue, "tr")).toBe(issue.message);
    expect(localizeWorkflowValidationIssue(issue, "en")).toBe(
      "The team-lead rule can only be used with team or team-and-role assignments.",
    );
    expect(issue.code).toBe("task.teamLead.assignment");
  });
});
