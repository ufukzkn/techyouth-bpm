import { describe, expect, it } from "vitest";
import {
  createLegacyFormLayout,
  fromFormDefinitionVersion,
  normalizeFormLayout,
  resolveFormPages,
  toCreateFormVersionRequest,
} from "@/features/forms/formVersioning";
import type { FormDefinition, FormDefinitionVersion, FormFieldDefinition } from "@/lib/types";

describe("form versioning", () => {
  it("normalizes an existing flat form to one published page", () => {
    const form = createForm([
      createField("second", 2),
      createField("first", 1),
    ]);

    expect(createLegacyFormLayout(form, "Page")).toEqual({
      version: 1,
      status: "published",
      pages: [
        {
          id: "page-1",
          title: "Page 1",
          description: "",
          fieldKeys: ["first", "second"],
        },
      ],
    });
  });

  it("keeps empty pages and assigns omitted fields to the first page", () => {
    const form = createForm([
      createField("first", 1),
      createField("second", 2),
      createField("third", 3),
    ]);
    const layout = normalizeFormLayout(
      form,
      {
        versionId: "version-3",
        version: 3,
        status: "draft",
        pages: [
          { id: "details", title: "Details", description: "", fieldKeys: ["second", "missing"] },
          { id: "review", title: "Review", description: "Check", fieldKeys: [] },
        ],
      },
      "Page",
    );

    expect(layout).toEqual({
      versionId: "version-3",
      version: 3,
      status: "draft",
      pages: [
        { id: "details", title: "Details", description: "", fieldKeys: ["second", "first", "third"] },
        { id: "review", title: "Review", description: "Check", fieldKeys: [] },
      ],
    });
  });

  it("resolves page field order from layout keys", () => {
    const form = createForm([createField("first", 1), createField("second", 2)]);
    const resolved = resolveFormPages(
      form,
      {
        version: 2,
        status: "published",
        pages: [
          { id: "two", title: "Second", description: "", fieldKeys: ["second"] },
          { id: "one", title: "First", description: "", fieldKeys: ["first"] },
        ],
      },
      "Page",
    );

    expect(resolved.pages.map((page) => page.fields.map((field) => field.key))).toEqual([["second"], ["first"]]);
  });

  it("maps API versions into the feature adapter shape", () => {
    const version: FormDefinitionVersion = {
      id: "version-4",
      formDefinitionId: "form-1",
      formName: "Form",
      versionNumber: 4,
      status: "Archived",
      createdByUserId: "user-1",
      createdAt: "2026-07-14T00:00:00Z",
      pages: [
        {
          id: "page-row-1",
          key: "details",
          title: "Details",
          description: "",
          sortOrder: 1,
          fields: [createField("second", 2), createField("first", 1)],
        },
      ],
    };

    const result = fromFormDefinitionVersion(version);

    expect(result.layout).toMatchObject({
      versionId: "version-4",
      version: 4,
      status: "archived",
      pages: [{ id: "details", fieldKeys: ["first", "second"] }],
    });
    expect(result.fields.map((field) => [field.key, field.sortOrder])).toEqual([
      ["first", 1],
      ["second", 2],
    ]);
  });

  it("builds nested API page requests from a designer snapshot", () => {
    const form = createForm([createField("first", 1), createField("second", 2)]);
    const request = toCreateFormVersionRequest({
      form,
      request: {
        name: form.name,
        description: form.description,
        fields: form.fields.map((field) => ({
          key: field.key,
          label: field.label,
          type: field.type,
          required: field.required,
          sortOrder: field.sortOrder,
          options: field.options,
          validationRules: field.validationRules,
        })),
      },
      layout: {
        version: 2,
        status: "draft",
        pages: [
          { id: "details", title: "Details", description: "", fieldKeys: ["second"] },
          { id: "review", title: "Review", description: "", fieldKeys: ["first"] },
        ],
      },
    });

    expect(request.pages.map((page) => ({ key: page.key, fields: page.fields.map((field) => field.key) }))).toEqual([
      { key: "details", fields: ["second"] },
      { key: "review", fields: ["first"] },
    ]);
  });
});

function createForm(fields: FormFieldDefinition[]): FormDefinition {
  return {
    id: "form-1",
    name: "Form",
    description: "",
    communityId: "community-1",
    communityName: "Community",
    createdByUserId: "user-1",
    createdAt: "2026-07-14T00:00:00Z",
    fields,
  };
}

function createField(key: string, sortOrder: number): FormFieldDefinition {
  return {
    id: key,
    key,
    label: key,
    type: "Text",
    required: false,
    sortOrder,
    options: [],
    validationRules: [],
  };
}
