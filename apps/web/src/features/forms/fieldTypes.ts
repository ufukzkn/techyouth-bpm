import type { FieldType, FormFieldDefinition } from "@/lib/types";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export const supportedFieldTypes = ["Text", "Number", "Email", "Select", "Checkbox", "Date"] as const satisfies readonly FieldType[];

export const fieldTypeLabels: Record<FieldType, string> = {
  Text: "Text",
  Number: "Number",
  Email: "Email",
  Select: "Select",
  Checkbox: "Checkbox",
  Date: "Date",
};

export function fieldTypeLabel(language: Language, fieldType: FieldType) {
  return translate(language, `form.type.${fieldType}` as TranslationKey);
}

export function fieldTypeUsesOptions(fieldType: FieldType) {
  return fieldType === "Select";
}

export function createDefaultOptions(fieldType: FieldType, language: Language = "tr") {
  return fieldTypeUsesOptions(fieldType) ? [translate(language, "form.defaultOption")] : [];
}

export function createFieldKey(label: string, fallbackIndex: number) {
  const safeKey = label
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .replace(/\s+([a-z0-9])/g, (_, char: string) => char.toUpperCase());

  return safeKey || `field${fallbackIndex}`;
}

export function createDefaultField({
  label,
  type,
  required,
  sortOrder,
  language = "tr",
}: {
  label: string;
  type: FieldType;
  required: boolean;
  sortOrder: number;
  language?: Language;
}): Omit<FormFieldDefinition, "id"> {
  const trimmedLabel = label.trim();
  const key = createFieldKey(trimmedLabel, sortOrder);

  return {
    key,
    label: trimmedLabel || translate(language, "form.defaultField", { sortOrder }),
    type,
    required,
    sortOrder,
    options: createDefaultOptions(type, language),
    validationRules: [],
  };
}
