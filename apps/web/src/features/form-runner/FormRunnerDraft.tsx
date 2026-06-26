"use client";

import { Play, RotateCcw } from "lucide-react";
import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from "react";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { FieldType, FormDefinition } from "@/lib/types";

type FormValues = Record<string, string | boolean>;

export function FormRunnerDraft() {
  const token = useSessionStore((state) => state.token);
  const [forms, setForms] = useState<FormDefinition[]>([]);
  const [selectedFormId, setSelectedFormId] = useState("");
  const [values, setValues] = useState<FormValues>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<"loading" | "idle" | "submitting" | "success" | "error">("loading");
  const [message, setMessage] = useState("Formlar yukleniyor.");

  const selectedForm = forms.find((form) => form.id === selectedFormId);

  const output = useMemo(
    () => ({
      formDefinitionId: selectedFormId,
      formData: values,
    }),
    [selectedFormId, values],
  );

  useEffect(() => {
    async function loadForms() {
      if (!token) {
        setStatus("error");
        setMessage("Formlari listelemek icin API oturumu gerekli.");
        return;
      }

      try {
        setStatus("loading");
        const result = await api.listForms(token);
        const nextSelectedForm = result[0];
        setForms(result);
        setSelectedFormId(nextSelectedForm?.id ?? "");
        setValues(nextSelectedForm ? buildInitialValues(nextSelectedForm) : {});
        setErrors({});
        setStatus("idle");
        setMessage(result.length > 0 ? "Kayitli formlar yuklendi." : "Once bir form tasarimi kaydedilmeli.");
      } catch (error) {
        setStatus("error");
        setMessage(error instanceof ApiError ? error.errors.join(" ") : "Formlar yuklenemedi.");
      }
    }

    void loadForms();
  }, [token]);

  function handleChange(fieldKey: string, fieldType: FieldType, event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    const value = fieldType === "Checkbox" ? (event.target as HTMLInputElement).checked : event.target.value;
    setValues((current) => ({ ...current, [fieldKey]: value }));
    setStatus("idle");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedForm || !token) {
      return;
    }

    const nextErrors = validate(selectedForm, values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setStatus("idle");
      setMessage("Formda duzeltilmesi gereken alanlar var.");
      return;
    }

    try {
      setStatus("submitting");
      const process = await api.startProcess(token, {
        formDefinitionId: selectedForm.id,
        formData: values,
      });
      setStatus("success");
      setMessage(`Surec baslatildi ve SQLite'a kaydedildi: ${process.id}`);
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Surec baslatilamadi.");
    }
  }

  function resetForm() {
    if (!selectedForm) {
      return;
    }

    setValues(buildInitialValues(selectedForm));
    setErrors({});
    setStatus("idle");
    setMessage("Form temizlendi.");
  }

  return (
    <section className="runner-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Formu Baslat</span>
          <h2>Dinamik veri girisi</h2>
        </div>
        <p>Kayitli form definition secilir, veriler backend validasyonundan gecerek surec instance olusturur.</p>
      </div>

      <div className="runner-grid">
        <form className="runner-form" onSubmit={handleSubmit}>
          <label>
            Kayitli form
            <select
              value={selectedFormId}
              onChange={(event) => {
                const nextForm = forms.find((form) => form.id === event.target.value);
                setSelectedFormId(event.target.value);
                setValues(nextForm ? buildInitialValues(nextForm) : {});
                setErrors({});
                setStatus("idle");
              }}
            >
              {forms.length === 0 ? <option value="">Kayitli form yok</option> : null}
              {forms.map((form) => (
                <option key={form.id} value={form.id}>
                  {form.name}
                </option>
              ))}
            </select>
          </label>

          {!selectedForm ? <p className="empty-state">Surec baslatmak icin once Form Tasarimi ekraninda form kaydet.</p> : null}

          {selectedForm?.fields
            .slice()
            .sort((first, second) => first.sortOrder - second.sortOrder)
            .map((field) => (
              <label className={field.type === "Checkbox" ? "checkbox-row runner-checkbox" : undefined} key={field.key}>
                {field.type === "Checkbox" ? (
                  <>
                    <input
                      checked={Boolean(values[field.key])}
                      onChange={(event) => handleChange(field.key, field.type, event)}
                      type="checkbox"
                    />
                    {field.label}
                  </>
                ) : (
                  <>
                    {field.label}
                    <FieldInput
                      fieldKey={field.key}
                      fieldType={field.type}
                      options={field.options}
                      value={values[field.key]}
                      onChange={handleChange}
                    />
                  </>
                )}
                {errors[field.key] ? <span className="field-error">{errors[field.key]}</span> : null}
              </label>
            ))}

          <p className={`status-line status-line-${status}`}>{message}</p>

          <div className="runner-actions">
            <button className="primary-button" disabled={!selectedForm || status === "submitting"} type="submit">
              <Play size={18} />
              {status === "submitting" ? "Baslatiliyor" : "Surec baslat"}
            </button>
            <button className="secondary-button" disabled={!selectedForm} type="button" onClick={resetForm}>
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
  fieldKey,
  fieldType,
  options,
  value,
  onChange,
}: {
  fieldKey: string;
  fieldType: FieldType;
  options: string[];
  value: string | boolean;
  onChange: (fieldKey: string, fieldType: FieldType, event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => void;
}) {
  if (fieldType === "Select") {
    return (
      <select value={String(value ?? "")} onChange={(event) => onChange(fieldKey, fieldType, event)}>
        <option value="">Seciniz</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  const inputType =
    fieldType === "Number" ? "number" : fieldType === "Email" ? "email" : fieldType === "Date" ? "date" : "text";

  return <input value={String(value ?? "")} onChange={(event) => onChange(fieldKey, fieldType, event)} type={inputType} />;
}

function buildInitialValues(form: FormDefinition) {
  return form.fields.reduce<FormValues>((current, field) => {
    current[field.key] = field.type === "Checkbox" ? false : "";
    return current;
  }, {});
}

function validate(form: FormDefinition, values: FormValues) {
  const nextErrors: Record<string, string> = {};

  for (const field of form.fields) {
    const value = values[field.key];
    const isEmpty = value === "" || value === false || value === undefined;

    if (field.required && isEmpty) {
      nextErrors[field.key] = `${field.label} zorunludur.`;
    }

    if (field.type === "Email" && String(value ?? "").length > 0 && !String(value).includes("@")) {
      nextErrors[field.key] = "Gecerli bir e-posta girilmelidir.";
    }

    for (const rule of field.validationRules) {
      if (
        rule.ruleType === "RequiredWhen" &&
        values[rule.dependsOnFieldKey] === rule.expectedValue &&
        String(value ?? "").trim().length === 0
      ) {
        nextErrors[field.key] = rule.message;
      }
    }
  }

  return nextErrors;
}
