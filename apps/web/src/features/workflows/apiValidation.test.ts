import { describe, expect, it } from "vitest";
import { workflowApiValidationIssues } from "@/features/workflows/apiValidation";
import { createStarterWorkflowDraft } from "@/features/workflows/workflowDraft";
import { ApiError } from "@/lib/api";

describe("workflowApiValidationIssues", () => {
  it("maps an unknown condition field error to the related gateway transition", () => {
    const draft = createStarterWorkflowDraft();
    const gatewayEdge = draft.edges.find((edge) => edge.id === "transition-gateway-complete")!;
    gatewayEdge.data.condition = {
      fieldKey: "start.amount",
      operator: "LessThanOrEquals",
      valueType: "Number",
      value: "50000",
    };
    const error = new ApiError(["Condition path 'start.amount' references an unknown form field."], 400);

    const issues = workflowApiValidationIssues(error, draft, "tr");

    expect(issues).toContainEqual(expect.objectContaining({
      entityId: "transition-gateway-complete",
      scope: "transition",
      severity: "error",
    }));
    expect(issues[0].message).toContain("start.amount");
  });
});
