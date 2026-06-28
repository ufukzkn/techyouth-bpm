"use client";

import type { ChangeEvent } from "react";
import type { FieldType, FormFieldDefinition } from "@/lib/types";
import type { FormValue } from "@/features/forms/formValues";

type FieldRendererProps = {
  field: FormFieldDefinition;
  value: FormValue;
  error?: string;
  onChange: (fieldKey: string, value: FormValue) => void;
};

export function FieldRenderer({ field, value, error, onChange }: FieldRendererProps) {
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
          <FieldInput field={field} value={value} onChange={onChange} />
        </>
      )}
      {error ? <span className="field-error">{error}</span> : null}
    </label>
  );
}

function FieldInput({
  field,
  value,
  onChange,
}: {
  field: FormFieldDefinition;
  value: FormValue;
  onChange: (fieldKey: string, value: FormValue) => void;
}) {
  if (field.type === "Select") {
    return (
      <select value={String(value ?? "")} onChange={(event) => onChange(field.key, event.target.value)}>
        <option value="">Seciniz</option>
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
