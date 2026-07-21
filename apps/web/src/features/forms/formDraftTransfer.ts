import {
  isSupportedFieldType,
  normalizeDesignerPages,
  type DesignerField,
  type DesignerPage,
} from "@/features/form-designer/formDesignerModel";
import type { ValidationRule } from "@/lib/types";

export const formDraftSchema = "techyouth.form-draft";
export const formDraftSchemaVersion = 1;

export type PortableFormDraft = {
  name: string;
  description: string;
  pages: DesignerPage[];
};

type FormDraftEnvelope = {
  schema: typeof formDraftSchema;
  schemaVersion: typeof formDraftSchemaVersion;
  exportedAt: string;
  draft: PortableFormDraft;
};

export function serializeFormDraft(draft: PortableFormDraft, exportedAt = new Date().toISOString()) {
  const envelope: FormDraftEnvelope = {
    schema: formDraftSchema,
    schemaVersion: formDraftSchemaVersion,
    exportedAt,
    draft: clonePortableDraft(draft),
  };

  return JSON.stringify(envelope, null, 2);
}

export function parseFormDraft(content: string): PortableFormDraft {
  const envelope = parseEnvelope(content);
  const draft = requireRecord(envelope.draft, "DRAFT_INVALID");
  const name = requireString(draft.name, "DRAFT_NAME_INVALID");
  const description = requireString(draft.description, "DRAFT_DESCRIPTION_INVALID");
  const rawPages = requireArray(draft.pages, "DRAFT_PAGES_INVALID");

  if (rawPages.length === 0 || rawPages.length > 50) {
    throw new Error("DRAFT_PAGES_INVALID");
  }

  let globalSortOrder = 0;
  const usedKeys = new Set<string>();
  const pages = rawPages.map((rawPage, pageIndex): DesignerPage => {
    const page = requireRecord(rawPage, "DRAFT_PAGE_INVALID");
    const title = requireString(page.title, "DRAFT_PAGE_INVALID").trim();
    const pageDescription = requireString(page.description, "DRAFT_PAGE_INVALID");
    const rawFields = requireArray(page.fields, "DRAFT_FIELDS_INVALID");
    if (!title || rawFields.length > 200) {
      throw new Error("DRAFT_PAGE_INVALID");
    }

    const fields = rawFields.map((rawField, fieldIndex): DesignerField => {
      const field = requireRecord(rawField, "DRAFT_FIELD_INVALID");
      const key = requireString(field.key, "DRAFT_FIELD_INVALID").trim();
      const label = requireString(field.label, "DRAFT_FIELD_INVALID").trim();
      if (!key || !label || usedKeys.has(key) || !isSupportedFieldType(field.type)) {
        throw new Error("DRAFT_FIELD_INVALID");
      }
      usedKeys.add(key);

      const options = requireArray(field.options, "DRAFT_FIELD_INVALID").map((option) =>
        requireString(option, "DRAFT_FIELD_INVALID"));
      const validationRules = requireArray(field.validationRules, "DRAFT_FIELD_INVALID").map(parseValidationRule);

      return {
        id: `import-field-${pageIndex + 1}-${fieldIndex + 1}`,
        key,
        label,
        type: field.type,
        required: requireBoolean(field.required, "DRAFT_FIELD_INVALID"),
        sortOrder: (globalSortOrder += 1),
        options,
        validationRules,
      };
    });

    return {
      id: `import-page-${pageIndex + 1}`,
      title,
      description: pageDescription,
      fields,
    };
  });

  return {
    name: name.trim() || "İçe Aktarılan Form",
    description,
    pages: normalizeDesignerPages(pages),
  };
}

function parseEnvelope(content: string) {
  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    throw new Error("DRAFT_JSON_INVALID");
  }

  const envelope = requireRecord(parsed, "DRAFT_INVALID");
  if (envelope.schema !== formDraftSchema || envelope.schemaVersion !== formDraftSchemaVersion) {
    throw new Error("DRAFT_SCHEMA_UNSUPPORTED");
  }
  return envelope;
}

function parseValidationRule(value: unknown): ValidationRule {
  const rule = requireRecord(value, "DRAFT_RULE_INVALID");
  if (rule.ruleType !== "RequiredWhen") {
    throw new Error("DRAFT_RULE_INVALID");
  }

  return {
    ruleType: "RequiredWhen",
    dependsOnFieldKey: requireString(rule.dependsOnFieldKey, "DRAFT_RULE_INVALID"),
    expectedValue: requireString(rule.expectedValue, "DRAFT_RULE_INVALID"),
    message: requireString(rule.message, "DRAFT_RULE_INVALID"),
  };
}

function clonePortableDraft(draft: PortableFormDraft): PortableFormDraft {
  return {
    name: draft.name,
    description: draft.description,
    pages: draft.pages.map((page) => ({
      id: page.id,
      title: page.title,
      description: page.description,
      fields: page.fields.map((field) => ({
        ...field,
        options: [...field.options],
        validationRules: field.validationRules.map((rule) => ({ ...rule })),
      })),
    })),
  };
}

function requireRecord(value: unknown, code: string): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error(code);
  return value as Record<string, unknown>;
}

function requireArray(value: unknown, code: string): unknown[] {
  if (!Array.isArray(value)) throw new Error(code);
  return value;
}

function requireString(value: unknown, code: string) {
  if (typeof value !== "string") throw new Error(code);
  return value;
}

function requireBoolean(value: unknown, code: string) {
  if (typeof value !== "boolean") throw new Error(code);
  return value;
}
