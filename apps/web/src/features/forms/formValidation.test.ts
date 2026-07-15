import { describe, expect, it } from "vitest";
import { validateFormFields } from "@/features/forms/formValidation";
import type { FormFieldDefinition } from "@/lib/types";

describe("page-scoped form validation", () => {
  it("validates only fields on the active page", () => {
    const first = createField("first", true);

    expect(validateFormFields([first], { first: "", second: "" }, "en")).toEqual({
      first: "first is required.",
    });
  });

  it("evaluates a rule using a value from another page", () => {
    const approvalNote: FormFieldDefinition = {
      ...createField("approvalNote", false),
      validationRules: [
        {
          ruleType: "RequiredWhen",
          dependsOnFieldKey: "requestType",
          expectedValue: "Purchase",
          message: "Approval note is required for purchases.",
        },
      ],
    };

    expect(
      validateFormFields(
        [approvalNote],
        { requestType: "Purchase", approvalNote: "" },
        "en",
      ),
    ).toEqual({ approvalNote: "Approval note is required for purchases." });
  });
});

function createField(key: string, required: boolean): FormFieldDefinition {
  return {
    id: key,
    key,
    label: key,
    type: "Text",
    required,
    sortOrder: 1,
    options: [],
    validationRules: [],
  };
}
