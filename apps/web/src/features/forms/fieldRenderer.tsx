"use client";

import type { ChangeEvent } from "react";
import { translate } from "@/features/i18n/translations";
import type { FieldType, FormFieldDefinition, Language } from "@/lib/types";
import type { FormValue } from "@/features/forms/formValues";

type FieldRendererProps = {
  field: FormFieldDefinition;
  value: FormValue;
  error?: string;
  language: Language;
  onChange: (fieldKey: string, value: FormValue) => void;
};

export function FieldRenderer({ field, value, error, language, onChange }: FieldRendererProps) {
  const labelClassName = field.type === "Checkbox" ? "checkbox-row runner-checkbox" : undefined;

  return (
    <label className={labelClassName}>
      {field.type === "Checkbox" ? (
        <>
          <input
            checked={Boolean(value)}
            onChange={(event) => onChange(field.key, event.target.checked)}
            type="checkbox"
          />
          {field.label}
        </>
      ) : (
        <>
          {field.label}
          <FieldInput field={field} value={value} language={language} onChange={onChange} />
        </>
      )}
      {error ? <span className="field-error">{error}</span> : null}
    </label>
  );
}

function FieldInput({
  field,
  value,
  language,
  onChange,
}: {
  field: FormFieldDefinition;
  value: FormValue;
  language: Language;
  onChange: (fieldKey: string, value: FormValue) => void;
}) {
  if (field.type === "Select") {
    return (
      <select value={String(value ?? "")} onChange={(event) => onChange(field.key, event.target.value)}>
        <option value="">{translate(language, "form.selectPlaceholder")}</option>
        {field.options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  return (
    <input
      value={String(value ?? "")}
      onChange={(event) => onChange(field.key, readInputValue(field.type, event))}
      type={toInputType(field.type)}
    />
  );
}

function toInputType(fieldType: FieldType) {
  return fieldType === "Number" ? "number" : fieldType === "Email" ? "email" : fieldType === "Date" ? "date" : "text";
}

function readInputValue(fieldType: FieldType, event: ChangeEvent<HTMLInputElement>) {
  if (fieldType !== "Number") {
    return event.target.value;
  }

  return event.target.value === "" ? "" : event.target.valueAsNumber;
}
