import { describe, expect, it } from "vitest";
import {
  formDraftSchema,
  parseFormDraft,
  serializeFormDraft,
} from "@/features/forms/formDraftTransfer";
import type { PortableFormDraft } from "@/features/forms/formDraftTransfer";

const sample: PortableFormDraft = {
  name: "Transfer Talebi",
  description: "Çok adımlı örnek",
  pages: [
    {
      id: "database-page-id",
      title: "Talep",
      description: "",
      fields: [
        {
          id: "database-field-id",
          key: "playerName",
          label: "Oyuncu",
          type: "Text",
          required: true,
          sortOrder: 42,
          options: [],
          validationRules: [],
        },
      ],
    },
  ],
};

describe("form draft transfer", () => {
  it("round-trips portable content while replacing persisted designer ids", () => {
    const imported = parseFormDraft(serializeFormDraft(sample, "2026-07-20T12:00:00.000Z"));

    expect(imported.name).toBe("Transfer Talebi");
    expect(imported.pages[0].id).toBe("import-page-1");
    expect(imported.pages[0].fields[0]).toMatchObject({
      id: "import-field-1-1",
      key: "playerName",
      sortOrder: 1,
    });
  });

  it("rejects unknown schemas and duplicate field keys", () => {
    expect(() => parseFormDraft(JSON.stringify({
      schema: `${formDraftSchema}.future`,
      schemaVersion: 1,
      draft: sample,
    }))).toThrow("DRAFT_SCHEMA_UNSUPPORTED");

    const duplicate = { ...sample, pages: [{ ...sample.pages[0], fields: [sample.pages[0].fields[0], sample.pages[0].fields[0]] }] };
    expect(() => parseFormDraft(serializeFormDraft(duplicate))).toThrow("DRAFT_FIELD_INVALID");
  });
});
