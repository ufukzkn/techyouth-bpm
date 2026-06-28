import type { FormDefinition } from "@/lib/types";
import type { FormValues } from "@/features/forms/formValues";

export type FormValidationErrors = Record<string, string>;

export function validateFormValues(form: FormDefinition, values: FormValues) {
  const nextErrors: FormValidationErrors = {};

  for (const field of form.fields) {
    const value = values[field.key];
    const isEmpty = isEmptyValue(value);

    if (field.required && isEmpty) {
      nextErrors[field.key] = `${field.label} zorunludur.`;
    }

    if (!isEmpty) {
      if (field.type === "Email" && !isValidEmail(String(value))) {
        nextErrors[field.key] = "Gecerli bir e-posta girilmelidir.";
      }

      if (field.type === "Number" && !isValidNumber(value)) {
        nextErrors[field.key] = "Gecerli bir sayi girilmelidir.";
      }

      if (field.type === "Date" && !isValidDate(String(value))) {
        nextErrors[field.key] = "Gecerli bir tarih girilmelidir.";
      }

      if (field.type === "Select" && typeof value === "string" && !field.options.includes(value)) {
        nextErrors[field.key] = "Listede tanimli bir secenek secilmelidir.";
      }

      if (field.type === "Checkbox" && typeof value !== "boolean") {
        nextErrors[field.key] = "Checkbox degeri true veya false olmalidir.";
      }
    }

    for (const rule of field.validationRules) {
      if (
        rule.ruleType === "RequiredWhen" &&
        String(values[rule.dependsOnFieldKey] ?? "") === rule.expectedValue &&
        isEmpty
      ) {
        nextErrors[field.key] = rule.message || `${field.label} zorunludur.`;
      }
    }
  }

  return nextErrors;
}

function isEmptyValue(value: FormValues[string]) {
  return value === "" || value === false || value === undefined || value === null;
}

function isValidEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

function isValidNumber(value: FormValues[string]) {
  if (typeof value === "number") {
    return Number.isFinite(value);
  }

  return typeof value === "string" && value.trim().length > 0 && Number.isFinite(Number(value));
}

function isValidDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value);
}
