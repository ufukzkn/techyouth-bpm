"use client";

import { Play, RotateCcw } from "lucide-react";
import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { buildInitialValues, prepareFormData, type FormValue, type FormValues } from "@/features/forms/formValues";
import { validateFormValues } from "@/features/forms/formValidation";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { FormDefinition } from "@/lib/types";

let formRunnerFormsCache: FormDefinition[] | null = null;

export function FormRunnerDraft() {
  const token = useSessionStore((state) => state.token);
  const language = useSessionStore((state) => state.language);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [forms, setForms] = useState<FormDefinition[]>(() => formRunnerFormsCache ?? []);
  const [selectedFormId, setSelectedFormId] = useState(() => formRunnerFormsCache?.[0]?.id ?? "");
  const [values, setValues] = useState<FormValues>(() =>
    formRunnerFormsCache?.[0] ? buildInitialValues(formRunnerFormsCache[0]) : {},
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "submitting" | "success" | "error">(
    formRunnerFormsCache ? "refreshing" : "loading",
  );
  const [message, setMessage] = useState(() =>
    formRunnerFormsCache ? t("form.runner.refreshingForms") : t("form.runner.loadingForms"),
  );
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
        setMessage(t("form.runner.sessionRequired"));
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
        setMessage(result.length > 0 ? t("form.runner.loadedForms") : t("form.runner.designFirst"));
      } catch (error) {
        setStatus("error");
        setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.runner.loadFailed"));
      }
    }

    void loadForms();
  }, [token, language, t]);

  function handleChange(fieldKey: string, value: FormValue) {
    setValues((current) => ({ ...current, [fieldKey]: value }));
    setStatus("idle");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedForm || !token) {
      return;
    }

    const nextErrors = validateFormValues(selectedForm, values, language);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setStatus("idle");
      setMessage(t("form.runner.fixFields"));
      return;
    }

    try {
      setStatus("submitting");
      const process = await api.startProcess(token, {
        formDefinitionId: selectedForm.id,
        formData: prepareFormData(selectedForm, values),
      });
      setStatus("success");
      setMessage(t("form.runner.started", { id: process.id }));
    } catch (error) {
      setStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.runner.startFailed"));
    }
  }

  function resetForm() {
    if (!selectedForm) {
      return;
    }

    setValues(buildInitialValues(selectedForm));
    setErrors({});
    setStatus("idle");
    setMessage(t("form.runner.cleared"));
  }

  return (
    <section className="runner-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("form.runner.eyebrow")}</span>
          <h2>{t("form.runner.title")}</h2>
        </div>
        <p>{t("form.runner.description")}</p>
      </div>

      <div className="runner-grid">
        <form className="runner-form" onSubmit={handleSubmit}>
          <label>
            {t("form.runner.savedForm")}
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
              {forms.length === 0 ? <option value="">{t("form.runner.noSavedForm")}</option> : null}
              {forms.map((form) => (
                <option key={form.id} value={form.id}>
                  {form.name}
                </option>
              ))}
            </select>
          </label>

          {status === "loading" ? <FormRunnerSkeleton language={language} /> : null}

          {!selectedForm && status !== "loading" ? (
            <p className="empty-state">{t("form.runner.noFormPrompt")}</p>
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
                language={language}
                onChange={handleChange}
              />
            ))}

          <p className={`status-line status-line-${status}`}>{message}</p>

          <div className="runner-actions">
            <button className="primary-button" disabled={!selectedForm || status === "submitting"} type="submit">
              <Play size={18} />
              {status === "submitting" ? t("form.runner.starting") : t("form.runner.startProcess")}
            </button>
            <button className="secondary-button" disabled={!selectedForm} type="button" onClick={resetForm}>
              <RotateCcw size={18} />
              {t("form.runner.clear")}
            </button>
          </div>
        </form>

        <pre className="json-preview runner-output">{JSON.stringify(output, null, 2)}</pre>
      </div>
    </section>
  );
}

function FormRunnerSkeleton({ language }: { language: "tr" | "en" }) {
  return (
    <div className="form-skeleton" aria-label={translate(language, "form.runner.skeleton")}>
      <span />
      <span />
      <span />
    </div>
  );
}
