"use client";

import { Play, RotateCcw } from "lucide-react";
import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { buildInitialValues, prepareFormData, type FormValue, type FormValues } from "@/features/forms/formValues";
import { validateFormValues } from "@/features/forms/formValidation";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { FormDefinition } from "@/lib/types";

let formRunnerFormsCache: FormDefinition[] | null = null;

export function FormRunnerDraft() {
  const token = useSessionStore((state) => state.token);
  const [forms, setForms] = useState<FormDefinition[]>(() => formRunnerFormsCache ?? []);
  const [selectedFormId, setSelectedFormId] = useState(() => formRunnerFormsCache?.[0]?.id ?? "");
  const [values, setValues] = useState<FormValues>(() =>
    formRunnerFormsCache?.[0] ? buildInitialValues(formRunnerFormsCache[0]) : {},
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "submitting" | "success" | "error">(
    formRunnerFormsCache ? "refreshing" : "loading",
  );
  const [message, setMessage] = useState(formRunnerFormsCache ? "Kayitli formlar guncelleniyor." : "Formlar yukleniyor.");
  const selectedFormIdRef = useRef(selectedFormId);

  const selectedForm = forms.find((form) => form.id === selectedFormId);

  const output = useMemo(() => {
    const formData = selectedForm ? prepareFormData(selectedForm, values) : values;

    return {
      formDefinitionId: selectedFormId,
      formData,
    };
  }, [selectedForm, selectedFormId, values]);

  useEffect(() => {
    selectedFormIdRef.current = selectedFormId;
  }, [selectedFormId]);

  useEffect(() => {
    async function loadForms() {
      if (!token) {
        setStatus("error");
        setMessage("Formlari listelemek icin API oturumu gerekli.");
        return;
      }

      try {
        setStatus(formRunnerFormsCache ? "refreshing" : "loading");
        const result = await api.listForms(token);
        formRunnerFormsCache = result;
        const currentSelection = result.find((form) => form.id === selectedFormIdRef.current);
        const nextSelectedForm = currentSelection ?? result[0];
        setForms(result);
        setSelectedFormId(nextSelectedForm?.id ?? "");
        setValues((current) =>
          currentSelection && Object.keys(current).length > 0 ? current : nextSelectedForm ? buildInitialValues(nextSelectedForm) : {},
        );
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

  function handleChange(fieldKey: string, value: FormValue) {
    setValues((current) => ({ ...current, [fieldKey]: value }));
    setStatus("idle");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedForm || !token) {
      return;
    }

    const nextErrors = validateFormValues(selectedForm, values);
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
        formData: prepareFormData(selectedForm, values),
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

          {status === "loading" ? <FormRunnerSkeleton /> : null}

          {!selectedForm && status !== "loading" ? (
            <p className="empty-state">Surec baslatmak icin once Form Tasarimi ekraninda form kaydet.</p>
          ) : null}

          {status !== "loading" && selectedForm?.fields
            .slice()
            .sort((first, second) => first.sortOrder - second.sortOrder)
            .map((field) => (
              <FieldRenderer
                key={field.key}
                field={field}
                value={values[field.key]}
                error={errors[field.key]}
                onChange={handleChange}
              />
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

function FormRunnerSkeleton() {
  return (
    <div className="form-skeleton" aria-label="Form alanlari yukleniyor">
      <span />
      <span />
      <span />
    </div>
  );
}
