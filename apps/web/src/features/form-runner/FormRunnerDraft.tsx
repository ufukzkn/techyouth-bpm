"use client";

import { Play, RotateCcw } from "lucide-react";
import { FormEvent, useEffect, useState } from "react";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { buildInitialValues, prepareFormData, type FormValue, type FormValues } from "@/features/forms/formValues";
import { validateFormValues } from "@/features/forms/formValidation";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { FormDefinition, ProcessDetail } from "@/lib/types";

type LoadStatus = "loading" | "idle" | "error";
type SubmitStatus = "idle" | "submitting" | "success" | "error";

export function FormRunnerDraft() {
  const token = useSessionStore((state) => state.token);
  const [forms, setForms] = useState<FormDefinition[]>([]);
  const [selectedFormId, setSelectedFormId] = useState("");
  const [values, setValues] = useState<FormValues>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [loadStatus, setLoadStatus] = useState<LoadStatus>("loading");
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>("idle");
  const [message, setMessage] = useState("Formlar yukleniyor.");
  const [submitResult, setSubmitResult] = useState<ProcessDetail | null>(null);

  const selectedForm = forms.find((form) => form.id === selectedFormId);
  const sortedFields = selectedForm?.fields.slice().sort((first, second) => first.sortOrder - second.sortOrder) ?? [];
  const errorCount = Object.keys(errors).length;
  const hasForms = forms.length > 0;

  const output = {
    formDefinitionId: selectedFormId,
    formData: selectedForm ? prepareFormData(selectedForm, values) : values,
  };

  useEffect(() => {
    async function loadForms() {
      if (!token) {
        setLoadStatus("error");
        setMessage("Formlari listelemek icin API oturumu gerekli.");
        return;
      }

      try {
        setLoadStatus("loading");
        const result = await api.listForms(token);
        const nextSelectedForm = result[0];
        setForms(result);
        setSelectedFormId(nextSelectedForm?.id ?? "");
        setValues(nextSelectedForm ? buildInitialValues(nextSelectedForm) : {});
        setErrors({});
        setSubmitResult(null);
        setLoadStatus("idle");
        setSubmitStatus("idle");
        setMessage(result.length > 0 ? "Kayitli formlar yuklendi." : "Once bir form tasarimi kaydedilmeli.");
      } catch (error) {
        setLoadStatus("error");
        setMessage(error instanceof ApiError ? error.errors.join(" ") : "Formlar yuklenemedi.");
      }
    }

    void loadForms();
  }, [token]);

  function handleChange(fieldKey: string, value: FormValue) {
    setValues((current) => ({ ...current, [fieldKey]: value }));
    setSubmitStatus("idle");
    setSubmitResult(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedForm || !token) {
      return;
    }

    const nextErrors = validateFormValues(selectedForm, values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setSubmitStatus("idle");
      setMessage("Formda duzeltilmesi gereken alanlar var.");
      return;
    }

    try {
      setSubmitStatus("submitting");
      setSubmitResult(null);
      const process = await api.startProcess(token, {
        formDefinitionId: selectedForm.id,
        formData: prepareFormData(selectedForm, values),
      });
      setSubmitStatus("success");
      setSubmitResult(process);
      setMessage("Surec baslatildi ve form verisi kaydedildi.");
    } catch (error) {
      setSubmitStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Surec baslatilamadi.");
    }
  }

  function resetForm() {
    if (!selectedForm) {
      return;
    }

    setValues(buildInitialValues(selectedForm));
    setErrors({});
    setSubmitStatus("idle");
    setSubmitResult(null);
    setMessage("Form temizlendi.");
  }

  return (
    <section className="runner-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Formu Baslat</span>
          <h2>Dinamik veri girisi</h2>
        </div>
        <p>Demo akisi: form sec, alanlari doldur, validation hatalarini gor, RequiredWhen kosulunu tetikle ve process start payload ciktisini kontrol et.</p>
      </div>

      <div className="runner-grid">
        <form className="runner-form" onSubmit={handleSubmit}>
          {loadStatus === "loading" ? (
            <div className="runner-state-panel" aria-live="polite">
              <strong>Formlar yukleniyor</strong>
              <span>Kayitli form definition listesi API uzerinden okunuyor. Birazdan form secimi hazir olacak.</span>
            </div>
          ) : null}

          {loadStatus === "error" ? (
            <div className="runner-state-panel runner-state-error" role="alert">
              <strong>Form listesi yuklenemedi</strong>
              <span>{message}</span>
            </div>
          ) : null}

          {loadStatus === "idle" && !hasForms ? (
            <div className="runner-state-panel">
              <strong>Kayitli form yok</strong>
              <span>Demo icin once Form Tasarimi ekraninda bir form kaydet; sonra burada secip doldurabilirsin.</span>
            </div>
          ) : null}

          <div className="runner-demo-guide">
            <strong>Demo adimlari</strong>
            <span>1. Form sec  2. Alanlari doldur  3. Hatalari kontrol et  4. Sureci baslat  5. JSON payloadi incele</span>
          </div>

          <label>
            Kayitli form            <select
              disabled={loadStatus !== "idle" || !hasForms || submitStatus === "submitting"}
              value={selectedFormId}
              onChange={(event) => {
                const nextForm = forms.find((form) => form.id === event.target.value);
                setSelectedFormId(event.target.value);
                setValues(nextForm ? buildInitialValues(nextForm) : {});
                setErrors({});
                setSubmitStatus("idle");
                setSubmitResult(null);
                setMessage(nextForm ? `${nextForm.name} secildi.` : "Form secimi temizlendi.");
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

          {selectedForm ? (
            <div className="selected-form-summary">
              <span className="eyebrow">1. Secili form</span>
              <strong>{selectedForm.name}</strong>
              <span>
                {sortedFields.length} alan - {selectedForm.description || "Aciklama yok"} - RequiredWhen kurallari varsa kosul saglandiginda alan bazinda hata gorunur.
              </span>
            </div>
          ) : null}

          {sortedFields.map((field) => (
            <FieldRenderer
              key={field.key}
              field={field}
              value={values[field.key]}
              error={errors[field.key]}
              onChange={handleChange}
            />
          ))}

          {errorCount > 0 ? (
            <div className="runner-state-panel runner-state-error" role="alert">
              <strong>{errorCount} alan kontrol edilmeli</strong>
              <span>Alan bazli hatalari duzeltmeden submit yapilmaz; API istegi engellendi.</span>
            </div>
          ) : null}

          {submitResult ? (
            <div className="runner-state-panel runner-state-success">
              <strong>Process start basarili</strong>
              <span>
                ID: {submitResult.id} - Status: {submitResult.status} - Started: {new Date(submitResult.startedAt).toLocaleString()}
              </span>
            </div>
          ) : null}

          <p className={`status-line status-line-${submitStatus}`}>{message}</p>

          <div className="runner-actions">
            <button className="primary-button" disabled={!selectedForm || submitStatus === "submitting"} type="submit">
              <Play size={18} />
              {submitStatus === "submitting" ? "Baslatiliyor" : "Surec baslat"}
            </button>
            <button className="secondary-button" disabled={!selectedForm || submitStatus === "submitting"} type="button" onClick={resetForm}>
              <RotateCcw size={18} />
              Temizle
            </button>
          </div>
        </form>

        <div className="runner-preview-panel">
          <div>
            <span className="eyebrow">5. Process start payload</span>
            <h3>APIye gonderilecek submitted form data JSON</h3>`n          </div>
          <pre className="json-preview runner-output">{JSON.stringify(output, null, 2)}</pre>
        </div>
      </div>
    </section>
  );
}