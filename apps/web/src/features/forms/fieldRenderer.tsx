"use client";

import { type ChangeEvent, useRef } from "react";
import { translate } from "@/features/i18n/translations";
import type { FieldType, FormFieldDefinition, Language } from "@/lib/types";
import type { FormValue } from "@/features/forms/formValues";
import { fileUploadAccept } from "@/features/forms/fieldTypes";

type FieldRendererProps = {
  field: FormFieldDefinition;
  value: FormValue;
  error?: string;
  language: Language;
  onChange: (fieldKey: string, value: FormValue) => void;
};

export function FieldRenderer({ field, value, error, language, onChange }: FieldRendererProps) {
  if (field.type === "Radio") {
    return (
      <fieldset className="runner-radio-group">
        <legend>{field.label}</legend>
        <FieldInput field={field} value={value} language={language} onChange={onChange} />
        {error ? <span className="field-error">{error}</span> : null}
      </fieldset>
    );
  }

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
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (field.type === "FileUpload") {
    const metadata = value && typeof value === "object" ? value : null;

    return (
      <div className="runner-file-upload">
        <div className="runner-file-picker">
          <label className="runner-file-button">
            {translate(language, "form.fileUpload.choose")}
            <input
              ref={fileInputRef}
              accept={fileUploadAccept}
              className="runner-file-native-input"
              onChange={(event) => {
                const file = event.currentTarget.files?.[0];
                onChange(
                  field.key,
                  file
                    ? { name: file.name, size: file.size, type: file.type, lastModified: file.lastModified }
                    : null,
                );
              }}
              type="file"
            />
          </label>
          <span className="runner-file-selection-state">
            {metadata ? metadata.name : translate(language, "form.fileUpload.noneSelected")}
          </span>
        </div>
        <span className="runner-file-note">{translate(language, "form.fileUpload.metadataNote")}</span>
        {metadata ? (
          <div className="runner-file-summary">
            <span>
              {translate(language, "form.fileUpload.selectedFile")}: <strong>{metadata.name}</strong>
            </span>
            <span>{formatFileSize(metadata.size)} · {metadata.type || "-"}</span>
            <button
              className="secondary-button"
              type="button"
              onClick={() => {
                if (fileInputRef.current) {
                  fileInputRef.current.value = "";
                }
                onChange(field.key, null);
              }}
            >
              {translate(language, "form.fileUpload.clear")}
            </button>
          </div>
        ) : null}
      </div>
    );
  }

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

  if (field.type === "Radio") {
    return (
      <div className="runner-radio-options">
        {field.options.map((option) => (
          <label className="checkbox-row runner-radio-option" key={option}>
            <input
              checked={String(value ?? "") === option}
              name={field.key}
              onChange={() => onChange(field.key, option)}
              type="radio"
              value={option}
            />
            {option}
          </label>
        ))}
      </div>
    );
  }

  if (field.type === "TextArea") {
    return (
      <textarea
        value={String(value ?? "")}
        onChange={(event) => onChange(field.key, event.target.value)}
        rows={4}
      />
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

function formatFileSize(size: number) {
  return size < 1024 * 1024 ? `${Math.ceil(size / 1024)} KB` : `${(size / (1024 * 1024)).toFixed(1)} MB`;
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
