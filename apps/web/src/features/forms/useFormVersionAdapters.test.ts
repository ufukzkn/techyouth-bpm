import { describe, expect, it } from "vitest";
import { formPersistenceToPages, formVersionToLayout } from "@/features/forms/useFormVersionAdapters";
import type { FormVersionPersistenceInput } from "@/features/forms/formVersioning";
import type { FormDefinitionVersion } from "@/lib/types";

describe("form version API adapter", () => {
  it("orders persisted pages and fields when rebuilding the editor layout", () => {
    const version = {
      id: "version-2",
      formDefinitionId: "form-1",
      formName: "Talep",
      versionNumber: 2,
      status: "Published",
      createdByUserId: "user-1",
      createdAt: "2026-07-14T10:00:00Z",
      pages: [
        { id: "page-db-2", key: "approval", title: "Onay", description: "", sortOrder: 1, fields: [] },
        {
          id: "page-db-1",
          key: "request",
          title: "Talep",
          description: "Bilgiler",
          sortOrder: 0,
          fields: [field("amount", 1), field("title", 0)],
        },
      ],
    } satisfies FormDefinitionVersion;

    expect(formVersionToLayout(version)).toEqual({
      versionId: "version-2",
      version: 2,
      status: "published",
      pages: [
        { id: "request", title: "Talep", description: "Bilgiler", fieldKeys: ["title", "amount"] },
        { id: "approval", title: "Onay", description: "", fieldKeys: [] },
      ],
    });
  });

  it("converts page field keys into complete API field definitions", () => {
    const title = field("title", 8);
    const amount = field("amount", 4);
    const input = {
      form: {
        id: "form-1",
        name: "Talep",
        description: "",
        communityId: "community-1",
        communityName: "Operasyon",
        createdByUserId: "user-1",
        createdAt: "2026-07-14T10:00:00Z",
        fields: [title, amount],
      },
      request: { name: "Talep", description: "", fields: [title, amount] },
      layout: {
        version: 1,
        status: "draft",
        pages: [
          { id: "request", title: "Talep", description: "", fieldKeys: ["amount"] },
          { id: "detail", title: "Detay", description: "", fieldKeys: ["title"] },
        ],
      },
    } satisfies FormVersionPersistenceInput;

    const pages = formPersistenceToPages(input);
    expect(pages.map((page) => page.key)).toEqual(["request", "detail"]);
    expect(pages[0].fields[0]).toMatchObject({ key: "amount", sortOrder: 0 });
    expect(pages[1].fields[0]).toMatchObject({ key: "title", sortOrder: 0 });
  });
});

function field(key: string, sortOrder: number) {
  return {
    key,
    label: key,
    type: "Text" as const,
    required: false,
    sortOrder,
    options: [],
    validationRules: [],
  };
}
