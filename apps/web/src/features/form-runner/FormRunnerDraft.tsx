"use client";

import { Play, RotateCcw } from "lucide-react";
import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { InlineValueLoader, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { buildInitialValues, prepareFormData, type FormValue, type FormValues } from "@/features/forms/formValues";
import { validateFormValues } from "@/features/forms/formValidation";
import { JsonViewer } from "@/features/ui/JsonViewer";
import { statusLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type { FormDefinition, ProcessDetail } from "@/lib/types";

type LoadStatus = "loading" | "refreshing" | "idle" | "error";
type SubmitStatus = "idle" | "submitting" | "success" | "error";

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
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(formRunnerFormsCache ? "refreshing" : "loading");
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>("idle");
  const [message, setMessage] = useState(() =>
    formRunnerFormsCache ? t("form.runner.refreshingForms") : t("form.runner.loadingForms"),
  );
  const [submitResult, setSubmitResult] = useState<ProcessDetail | null>(null);
  const selectedFormIdRef = useRef(selectedFormId);

  const selectedForm = forms.find((form) => form.id === selectedFormId);
  const sortedFields = selectedForm?.fields.slice().sort((first, second) => first.sortOrder - second.sortOrder) ?? [];
  const errorCount = Object.keys(errors).length;
  const hasForms = forms.length > 0;
  const isRunnerReady = Boolean(token) && loadStatus === "idle";
  const isActionDisabled = !selectedForm || !isRunnerReady || submitStatus === "submitting";

  const output = {
    formDefinitionId: selectedFormId,
    formData: selectedForm ? prepareFormData(selectedForm, values) : values,
  };

  useEffect(() => {
    selectedFormIdRef.current = selectedFormId;
  }, [selectedFormId]);

  useEffect(() => {
    let ignore = false;

    async function loadForms() {
      if (!token) {
        setLoadStatus("error");
        setMessage(t("form.runner.sessionRequired"));
        return;
      }

      try {
        setLoadStatus(formRunnerFormsCache ? "refreshing" : "loading");
        const result = await api.listForms(token);
        if (ignore) {
          return;
        }

        formRunnerFormsCache = result;
        const currentSelection = result.find((form) => form.id === selectedFormIdRef.current);
        const nextSelectedForm = currentSelection ?? result[0];
        setForms(result);
        setSelectedFormId(nextSelectedForm?.id ?? "");
        setValues((current) =>
          currentSelection && Object.keys(current).length > 0 ? current : nextSelectedForm ? buildInitialValues(nextSelectedForm) : {},
        );
        setErrors({});
        setSubmitResult(null);
        setLoadStatus("idle");
        setSubmitStatus("idle");
        setMessage(result.length > 0 ? t("form.runner.loadedForms") : t("form.runner.designFirst"));
      } catch (error) {
        if (ignore) {
          return;
        }

        setLoadStatus("error");
        setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.runner.loadFailed"));
      }
    }

    void loadForms();

    return () => {
      ignore = true;
    };
  }, [token, language, t]);

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

    const nextErrors = validateFormValues(selectedForm, values, language);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setSubmitStatus("idle");
      setMessage(t("form.runner.fixFields"));
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
      setMessage(t("form.runner.started", { id: process.id }));
    } catch (error) {
      setSubmitStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.runner.startFailed"));
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
    setMessage(t("form.runner.cleared"));
  }

  return (
    <section className={`runner-section${loadStatus === "loading" ? " runner-section-initial-loading" : ""}`}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("form.runner.eyebrow")}</span>
          <h2>{t("form.runner.title")}</h2>
        </div>
        <p>{t("form.runner.description")}</p>
      </div>

      {loadStatus === "loading" ? <FormRunnerSkeleton language={language} /> : null}

      <div className="runner-grid">
        <form className="runner-form" onSubmit={handleSubmit}>
          {loadStatus === "error" ? (
            <div className="runner-state-panel runner-state-error" role="alert">
              <strong>{token ? t("form.runner.loadFailed") : t("form.runner.sessionRequired")}</strong>
              <span>{message}</span>
            </div>
          ) : null}

          {loadStatus === "idle" && !hasForms ? (
            <div className="runner-state-panel">
              <strong>{t("form.runner.noSavedForm")}</strong>
              <span>{t("form.runner.noFormPrompt")}</span>
            </div>
          ) : null}

          <div className="runner-demo-guide">
            <strong>{t("form.runner.demoGuideTitle")}</strong>
            <span>{t("form.runner.demoGuideSteps")}</span>
          </div>

          <label>
            {t("form.runner.savedForm")}
            <select
              disabled={loadStatus !== "idle" || !hasForms || submitStatus === "submitting"}
              value={selectedFormId}
              onChange={(event) => {
                const nextForm = forms.find((form) => form.id === event.target.value);
                setSelectedFormId(event.target.value);
                setValues(nextForm ? buildInitialValues(nextForm) : {});
                setErrors({});
                setSubmitStatus("idle");
                setSubmitResult(null);
                setMessage(nextForm ? t("form.runner.selectedMessage", { name: nextForm.name }) : t("form.runner.noSavedForm"));
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

          {selectedForm ? (
            <div className="selected-form-summary">
              <span className="eyebrow">{t("form.runner.selectedSummaryEyebrow")}</span>
              <strong>{selectedForm.name}</strong>
              <span>
                {t("form.runner.selectedSummary", {
                  count: sortedFields.length,
                  description: selectedForm.description || t("form.runner.noDescription"),
                })}
              </span>
            </div>
          ) : null}

          {loadStatus !== "loading" && !selectedForm ? <p className="empty-state">{t("form.runner.noFormPrompt")}</p> : null}

          {loadStatus !== "loading" &&
            sortedFields.map((field) => (
              <FieldRenderer
                key={field.key}
                field={field}
                value={values[field.key]}
                error={errors[field.key]}
                language={language}
                onChange={handleChange}
              />
            ))}

          {errorCount > 0 ? (
            <div className="runner-state-panel runner-state-error" role="alert">
              <strong>{t("form.runner.validationBlockedTitle", { count: errorCount })}</strong>
              <span>{t("form.runner.validationBlockedDescription")}</span>
            </div>
          ) : null}

          {submitResult ? (
            <div className="runner-state-panel runner-state-success">
              <strong>{t("form.runner.successTitle")}</strong>
              <span>
                {t("form.runner.successSummary", {
                  id: submitResult.id,
                  status: statusLabel(language, submitResult.status),
                  startedAt: submitResult.startedAt ? formatApiDateTime(submitResult.startedAt, language) : "-",
                })}
              </span>
            </div>
          ) : null}

          <p className={`status-line status-line-${submitStatus}`} aria-live="polite">
            {message}
          </p>

          <div className="runner-actions">
            <button className="primary-button" disabled={isActionDisabled} type="submit">
              <Play size={18} />
              {submitStatus === "submitting" ? t("form.runner.starting") : t("form.runner.startProcess")}
            </button>
            <button className="secondary-button" disabled={isActionDisabled} type="button" onClick={resetForm}>
              <RotateCcw size={18} />
              {t("form.runner.clear")}
            </button>
          </div>
        </form>

        <div className="runner-preview-panel">
          <div>
            <span className="eyebrow">{t("form.runner.payloadEyebrow")}</span>
            <h3>{t("form.runner.payloadTitle")}</h3>
          </div>
          <JsonViewer className="runner-output" language={language} value={output} />
        </div>
      </div>
    </section>
  );
}

function FormRunnerSkeleton({ language }: { language: "tr" | "en" }) {
  const label = translate(language, "form.runner.loadingForms");

  return (
    <div className="form-opening-skeleton form-runner-opening-skeleton" role="status" aria-label={label}>
      <div className="form-opening-heading">
        <InlineValueLoader label={label} />
        <strong>{label}</strong>
      </div>
      <div className="form-opening-grid">
        <div className="form-opening-panel">
          <SkeletonBlock className="form-opening-title" />
          <SkeletonBlock className="form-opening-control" />
          <SkeletonBlock className="form-opening-summary" />
          <SkeletonBlock className="form-opening-control" />
          <SkeletonBlock className="form-opening-control" />
        </div>
        <div className="form-opening-panel form-opening-preview">
          <SkeletonBlock className="form-opening-title" />
          <SkeletonBlock className="form-opening-preview-block" />
        </div>
      </div>
    </div>
  );
}
