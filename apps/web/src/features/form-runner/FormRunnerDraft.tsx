"use client";

import { Play, RotateCcw } from "lucide-react";
import { ChangeEvent, FormEvent, useMemo, useState } from "react";

type FieldType = "Text" | "Number" | "Email" | "Select" | "Checkbox" | "Date";
type FormValues = Record<string, string | boolean>;

type RunnerField = {
  key: string;
  label: string;
  type: FieldType;
  required: boolean;
  options?: string[];
};

const fields: RunnerField[] = [
  { key: "customerName", label: "Musteri adi", type: "Text", required: true },
  { key: "requestType", label: "Talep tipi", type: "Select", required: true, options: ["Izin", "Masraf", "Satinalma"] },
  { key: "requestAmount", label: "Tutar", type: "Number", required: false },
  { key: "contactEmail", label: "E-posta", type: "Email", required: true },
  { key: "approvalNote", label: "Onay aciklamasi", type: "Text", required: false },
  { key: "acceptedTerms", label: "Bilgiler dogru", type: "Checkbox", required: true },
];

const initialValues: FormValues = {
  customerName: "Eczacibasi Demo",
  requestType: "Satinalma",
  requestAmount: "12000",
  contactEmail: "demo@eczacibasi.com",
  approvalNote: "",
  acceptedTerms: false,
};

export function FormRunnerDraft() {
  const [values, setValues] = useState<FormValues>(initialValues);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitState, setSubmitState] = useState<"idle" | "success">("idle");

  const output = useMemo(
    () => ({
      formDefinitionId: "demo-form-definition",
      formData: values,
    }),
    [values],
  );

  function handleChange(field: RunnerField, event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    const value = field.type === "Checkbox" ? (event.target as HTMLInputElement).checked : event.target.value;
    setValues((current) => ({ ...current, [field.key]: value }));
    setSubmitState("idle");
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setSubmitState(Object.keys(nextErrors).length === 0 ? "success" : "idle");
  }

  function resetForm() {
    setValues(initialValues);
    setErrors({});
    setSubmitState("idle");
  }

  return (
    <section className="runner-section" id="runner">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Formu Baslat</span>
          <h2>Dinamik veri girisi</h2>
        </div>
        <p>Bu taslak, tasarlanan formun kullanici tarafinda nasil doldurulacagini ve valide edilecegini gosterir.</p>
      </div>

      <div className="runner-grid">
        <form className="runner-form" onSubmit={handleSubmit}>
          {fields.map((field) => (
            <label className={field.type === "Checkbox" ? "checkbox-row runner-checkbox" : undefined} key={field.key}>
              {field.type === "Checkbox" ? (
                <>
                  <input
                    checked={Boolean(values[field.key])}
                    onChange={(event) => handleChange(field, event)}
                    type="checkbox"
                  />
                  {field.label}
                </>
              ) : (
                <>
                  {field.label}
                  <FieldInput field={field} value={values[field.key]} onChange={handleChange} />
                </>
              )}
              {errors[field.key] ? <span className="field-error">{errors[field.key]}</span> : null}
            </label>
          ))}

          {submitState === "success" ? <div className="form-success">Form verisi surec baslatmaya hazir.</div> : null}

          <div className="runner-actions">
            <button className="primary-button" type="submit">
              <Play size={18} />
              Surec baslat
            </button>
            <button className="secondary-button" type="button" onClick={resetForm}>
              <RotateCcw size={18} />
              Temizle
            </button>
          </div>
        </form>

        <pre className="json-preview runner-output">{JSON.stringify(output, null, 2)}</pre>
      </div>
    </section>
  );
}

function FieldInput({
  field,
  value,
  onChange,
}: {
  field: RunnerField;
  value: string | boolean;
  onChange: (field: RunnerField, event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => void;
}) {
  if (field.type === "Select") {
    return (
      <select value={String(value ?? "")} onChange={(event) => onChange(field, event)}>
        <option value="">Seciniz</option>
        {field.options?.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  const inputType =
    field.type === "Number" ? "number" : field.type === "Email" ? "email" : field.type === "Date" ? "date" : "text";

  return <input value={String(value ?? "")} onChange={(event) => onChange(field, event)} type={inputType} />;
}

function validate(values: FormValues) {
  const nextErrors: Record<string, string> = {};

  for (const field of fields) {
    const value = values[field.key];
    const isEmpty = value === "" || value === false || value === undefined;

    if (field.required && isEmpty) {
      nextErrors[field.key] = `${field.label} zorunludur.`;
    }
  }

  if (String(values.contactEmail ?? "").length > 0 && !String(values.contactEmail).includes("@")) {
    nextErrors.contactEmail = "Gecerli bir e-posta girilmelidir.";
  }

  if (values.requestType === "Satinalma" && String(values.approvalNote ?? "").trim().length === 0) {
    nextErrors.approvalNote = "Satinalma taleplerinde onay aciklamasi zorunludur.";
  }

  return nextErrors;
}
