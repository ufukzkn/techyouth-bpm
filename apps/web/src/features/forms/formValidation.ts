import type { FormDefinition } from "@/lib/types";
import type { FormValues } from "@/features/forms/formValues";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";
import type { FileUploadMetadata } from "@/lib/types";
import {
  fileUploadAllowedExtensions,
  fileUploadAllowedMimeTypes,
  fileUploadMaxSizeBytes,
} from "@/features/forms/fieldTypes";

export type FormValidationErrors = Record<string, string>;

export function validateFormValues(form: FormDefinition, values: FormValues, language: Language = "tr") {
  const nextErrors: FormValidationErrors = {};

  for (const field of form.fields) {
    const value = values[field.key];
    const isEmpty = isEmptyValue(value, field.type);

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

      if ((field.type === "Select" || field.type === "Radio") && (typeof value !== "string" || !field.options.includes(value))) {
        nextErrors[field.key] = translate(language, "form.validation.select");
      }

      if ((field.type === "Text" || field.type === "TextArea") && typeof value !== "string") {
        nextErrors[field.key] = translate(language, "form.validation.text");
      }

      if (field.type === "Checkbox" && typeof value !== "boolean") {
        nextErrors[field.key] = translate(language, "form.validation.checkbox");
      }

      if (field.type === "FileUpload") {
        const fileError = validateFileUploadMetadata(value, language);
        if (fileError) {
          nextErrors[field.key] = fileError;
        }
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

function isEmptyValue(value: FormValues[string], fieldType: FormDefinition["fields"][number]["type"]) {
  return value === "" || value === undefined || value === null || (fieldType === "Checkbox" && value === false);
}

function validateFileUploadMetadata(value: FormValues[string], language: Language) {
  if (!isFileUploadMetadata(value)) {
    return translate(language, "form.validation.fileMetadata");
  }

  if (value.size > fileUploadMaxSizeBytes) {
    return translate(language, "form.validation.fileMaxSize");
  }

  if (!fileUploadAllowedMimeTypes.includes(value.type as (typeof fileUploadAllowedMimeTypes)[number])) {
    return translate(language, "form.validation.fileType");
  }

  const extension = value.name.split(".").pop()?.toLocaleLowerCase("en-US") ?? "";
  if (!fileUploadAllowedExtensions.includes(extension as (typeof fileUploadAllowedExtensions)[number])) {
    return translate(language, "form.validation.fileExtension");
  }

  return undefined;
}

function isFileUploadMetadata(value: FormValues[string]): value is FileUploadMetadata {
  return Boolean(
    value
      && typeof value === "object"
      && typeof value.name === "string"
      && value.name.trim().length > 0
      && typeof value.size === "number"
      && Number.isInteger(value.size)
      && value.size > 0
      && typeof value.type === "string"
      && typeof value.lastModified === "number"
      && Number.isInteger(value.lastModified)
      && value.lastModified >= 0,
  );
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
