import type { FieldType, FormFieldDefinition } from "@/lib/types";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export const supportedFieldTypes = [
  "Text",
  "TextArea",
  "Number",
  "Email",
  "Select",
  "Radio",
  "Checkbox",
  "Date",
  "FileUpload",
] as const satisfies readonly FieldType[];

export const fieldTypeLabels: Record<FieldType, string> = {
  Text: "Text",
  TextArea: "Text Area",
  Number: "Number",
  Email: "Email",
  Select: "Select",
  Radio: "Radio Button",
  Checkbox: "Checkbox",
  Date: "Date",
  FileUpload: "File Upload",
};

export const fileUploadMaxSizeBytes = 10 * 1024 * 1024;
export const fileUploadAllowedExtensions = ["pdf", "png", "jpg", "jpeg", "doc", "docx", "xls", "xlsx"] as const;
export const fileUploadAllowedMimeTypes = [
  "application/pdf",
  "image/png",
  "image/jpeg",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
] as const;

export const fileUploadAccept = [
  ...fileUploadAllowedExtensions.map((extension) => `.${extension}`),
  ...fileUploadAllowedMimeTypes,
].join(",");

export function fieldTypeLabel(language: Language, fieldType: FieldType) {
  return translate(language, `form.type.${fieldType}` as TranslationKey);
}

export function fieldTypeUsesOptions(fieldType: FieldType) {
  return fieldType === "Select" || fieldType === "Radio";
}

export function createDefaultOptions(fieldType: FieldType, language: Language = "tr") {
  return fieldTypeUsesOptions(fieldType) ? [translate(language, "form.defaultOption")] : [];
}

export function createFieldKey(label: string, fallbackIndex: number) {
  const asciiLabel = label
    .trim()
    .toLowerCase()
    .replaceAll("ç", "c")
    .replaceAll("ğ", "g")
    .replaceAll("ı", "i")
    .replaceAll("i̇", "i")
    .replaceAll("ö", "o")
    .replaceAll("ş", "s")
    .replaceAll("ü", "u");

  const safeKey = asciiLabel
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
