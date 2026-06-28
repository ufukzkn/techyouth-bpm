import type { FieldType, FormFieldDefinition } from "@/lib/types";

export const supportedFieldTypes = ["Text", "Number", "Email", "Select", "Checkbox", "Date"] as const satisfies readonly FieldType[];

export const fieldTypeLabels: Record<FieldType, string> = {
  Text: "Text",
  Number: "Number",
  Email: "Email",
  Select: "Select",
  Checkbox: "Checkbox",
  Date: "Date",
};

export function fieldTypeUsesOptions(fieldType: FieldType) {
  return fieldType === "Select" || fieldType === "Checkbox";
}

export function createDefaultOptions(fieldType: FieldType) {
  return fieldTypeUsesOptions(fieldType) ? ["Secenek A"] : [];
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
}: {
  label: string;
  type: FieldType;
  required: boolean;
  sortOrder: number;
}): Omit<FormFieldDefinition, "id"> {
  const trimmedLabel = label.trim();
  const key = createFieldKey(trimmedLabel, sortOrder);

  return {
    key,
    label: trimmedLabel || `Alan ${sortOrder}`,
    type,
    required,
    sortOrder,
    options: createDefaultOptions(type),
    validationRules: [],
  };
}
