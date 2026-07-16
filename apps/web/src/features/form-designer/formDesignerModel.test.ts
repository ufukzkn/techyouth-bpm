import { describe, expect, it } from "vitest";
import {
  createVersionedLayout,
  flattenDesignerFields,
  moveFieldBetweenPages,
  removeDesignerPage,
  validateDesignerFields,
  type DesignerField,
  type DesignerPage,
} from "@/features/form-designer/formDesignerModel";

function field(id: string, key = id, sortOrder = 1): DesignerField {
  return {
    id,
    key,
    label: id,
    type: "Text",
    required: false,
    sortOrder,
    options: [],
    validationRules: [],
  };
}

function pages(): DesignerPage[] {
  return [
    { id: "page-a", title: "A", description: "", fields: [field("first", "first", 1)] },
    { id: "page-b", title: "B", description: "", fields: [field("second", "second", 2)] },
  ];
}

describe("formDesignerModel", () => {
  it("moves a field between pages and normalizes global sort order", () => {
    const result = moveFieldBetweenPages(pages(), "first", "page-b");

    expect(result[0].fields).toHaveLength(0);
    expect(result[1].fields.map((item) => item.id)).toEqual(["second", "first"]);
    expect(flattenDesignerFields(result).map((item) => item.sortOrder)).toEqual([1, 2]);
  });

  it("moves fields to the selected destination before removing a page", () => {
    const result = removeDesignerPage(pages(), "page-a", "page-b");

    expect(result).toHaveLength(1);
    expect(result[0].fields.map((item) => item.id)).toEqual(["second", "first"]);
  });

  it("keeps page field keys aligned with the flattened request payload", () => {
    const sourcePages = pages();
    const fields = flattenDesignerFields(sourcePages);
    const layout = createVersionedLayout(
      sourcePages,
      {
        name: "Demo",
        description: "",
        fields: fields.map((item) => ({
          key: item.key,
          label: item.label,
          type: item.type,
          required: item.required,
          sortOrder: item.sortOrder,
          options: item.options,
          validationRules: item.validationRules,
        })),
      },
      { versionId: "version-1", version: 2, status: "draft" },
    );

    expect(layout.pages.map((page) => page.fieldKeys)).toEqual([["first"], ["second"]]);
    expect(layout.versionId).toBe("version-1");
  });

  it("rejects duplicate field keys across different pages", () => {
    const duplicated = pages();
    duplicated[1].fields[0] = field("second", "first", 2);

    const errors = validateDesignerFields(flattenDesignerFields(duplicated), "tr");

    expect(errors.first.key).toBeTruthy();
    expect(errors.second.key).toBeTruthy();
  });
});
