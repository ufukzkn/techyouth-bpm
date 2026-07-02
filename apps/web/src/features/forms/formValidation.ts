import type { FormDefinition } from "@/lib/types";
import type { FormValues } from "@/features/forms/formValues";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export type FormValidationErrors = Record<string, string>;

export function validateFormValues(form: FormDefinition, values: FormValues, language: Language = "tr") {
  const nextErrors: FormValidationErrors = {};

  for (const field of form.fields) {
    const value = values[field.key];
    const isEmpty = isEmptyValue(value);

    if (field.required && isEmpty) {
      nextErrors[field.key] = translate(language, "form.validation.required", { label: field.label });
    }

    if (!isEmpty) {
      if (field.type === "Email" && !isValidEmail(String(value))) {
        nextErrors[field.key] = translate(language, "form.validation.email");
      }

      if (field.type === "Number" && !isValidNumber(value)) {
        nextErrors[field.key] = translate(language, "form.validation.number");
      }

      if (field.type === "Date" && !isValidDate(String(value))) {
        nextErrors[field.key] = translate(language, "form.validation.date");
      }

      if (field.type === "Select" && typeof value === "string" && !field.options.includes(value)) {
        nextErrors[field.key] = translate(language, "form.validation.select");
      }

      if (field.type === "Checkbox" && typeof value !== "boolean") {
        nextErrors[field.key] = translate(language, "form.validation.checkbox");
      }
    }

    for (const rule of field.validationRules) {
      if (
        rule.ruleType === "RequiredWhen" &&
        String(values[rule.dependsOnFieldKey] ?? "") === rule.expectedValue &&
        isEmpty
      ) {
        nextErrors[field.key] = rule.message || translate(language, "form.validation.required", { label: field.label });
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
