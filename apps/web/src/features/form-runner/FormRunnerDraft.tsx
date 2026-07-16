"use client";

import { ChevronLeft, ChevronRight, Play, RotateCcw } from "lucide-react";
import { useRouter } from "next/navigation";
import { type FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { InlineValueLoader, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { buildInitialValues, prepareFormData, type FormValue, type FormValues } from "@/features/forms/formValues";
import { validateFormFields, validateFormValues } from "@/features/forms/formValidation";
import { formatPagingCopy, getFormPagingCopy } from "@/features/forms/formPagingCopy";
import {
  resolveFormPages,
  type FormVersionAdapter,
  type FormVersionStatus,
} from "@/features/forms/formVersioning";
import { JsonViewer } from "@/features/ui/JsonViewer";
import { statusLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type { FormDefinition, ProcessDetail, RunnableProcessDefinition } from "@/lib/types";

type LoadStatus = "loading" | "refreshing" | "idle" | "error";
type SubmitStatus = "idle" | "submitting" | "success" | "error";
type FormProcessingStatus = "switching" | "clearing" | null;

const FORM_PROCESSING_DURATION_MS = 240;

let formRunnerFormsCache: FormDefinition[] | null = null;
let formRunnerWorkflowsCache: RunnableProcessDefinition[] | null = null;

export type FormRunnerDraftProps = {
  versionAdapter?: Pick<FormVersionAdapter, "resolveLayout" | "resolveVersion">;
};

export function FormRunnerDraft({ versionAdapter }: FormRunnerDraftProps = {}) {
  const router = useRouter();
  const token = useSessionStore((state) => state.token);
  const language = useSessionStore((state) => state.language);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const pagingCopy = getFormPagingCopy(language);
  const [forms, setForms] = useState<FormDefinition[]>(() => formRunnerFormsCache ?? []);
  const [workflows, setWorkflows] = useState<RunnableProcessDefinition[]>(() => formRunnerWorkflowsCache ?? []);
  const [selectedFormId, setSelectedFormId] = useState(() => formRunnerFormsCache?.[0]?.id ?? "");
  const [selectedWorkflowVersionId, setSelectedWorkflowVersionId] = useState("");
  const [values, setValues] = useState<FormValues>(() =>
    formRunnerFormsCache?.[0] ? buildInitialValues(resolveRunnerForm(formRunnerFormsCache[0], versionAdapter)) : {},
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(formRunnerFormsCache ? "refreshing" : "loading");
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>("idle");
  const [formProcessingStatus, setFormProcessingStatus] = useState<FormProcessingStatus>(null);
  const [message, setMessage] = useState(() =>
    formRunnerFormsCache ? t("form.runner.refreshingForms") : t("form.runner.loadingForms"),
  );
  const [submitResult, setSubmitResult] = useState<ProcessDetail | null>(null);
  const [activePageIndex, setActivePageIndex] = useState(0);
  const [furthestPageIndex, setFurthestPageIndex] = useState(0);
  const selectedFormIdRef = useRef(selectedFormId);
  const formProcessingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const submitInFlightRef = useRef(false);

  const selectedForm = forms.find((form) => form.id === selectedFormId);
  const adapterVersion = selectedForm ? versionAdapter?.resolveVersion?.(selectedForm) : null;
  const selectedFormVersionId = adapterVersion?.layout.versionId ?? selectedForm?.latestPublishedVersionId ?? null;
  const availableWorkflows = workflows.filter((workflow) => workflow.formDefinitionVersionId === selectedFormVersionId);
  const selectedWorkflow = availableWorkflows.find(
    (workflow) => workflow.processDefinitionVersionId === selectedWorkflowVersionId,
  ) ?? null;
  const activeFormDefinition = selectedForm
    ? adapterVersion
      ? { ...selectedForm, fields: adapterVersion.fields }
      : selectedForm
    : undefined;
  const sortedFields = activeFormDefinition?.fields.slice().sort((first, second) => first.sortOrder - second.sortOrder) ?? [];
  const resolvedPages = activeFormDefinition
    ? resolveFormPages(
        activeFormDefinition,
        adapterVersion?.layout ?? versionAdapter?.resolveLayout?.(activeFormDefinition),
        pagingCopy.page,
      )
    : null;
  const pages = resolvedPages?.pages ?? [];
  const activePage = pages[activePageIndex] ?? pages[0];
  const activeFields = activePage?.fields ?? sortedFields;
  const activeFieldKeys = new Set(activeFields.map((field) => field.key));
  const errorCount = Object.keys(errors).filter((fieldKey) => activeFieldKeys.has(fieldKey)).length;
  const hasForms = forms.length > 0;
  const isRunnerReady = Boolean(token) && loadStatus === "idle";
  const isFormProcessing = formProcessingStatus !== null;
  const isNavigationDisabled = !selectedForm || !isRunnerReady || submitStatus === "submitting" || isFormProcessing;
  const versionStatus = resolvedPages?.layout.status;
  const isUnavailableVersion = Boolean(versionStatus && versionStatus !== "published");
  const unavailableVersionMessage =
    versionStatus === "archived" ? pagingCopy.archivedUnavailable : pagingCopy.draftUnavailable;
  const isSubmitDisabled = isNavigationDisabled || isUnavailableVersion;
  const isLastPage = activePageIndex >= pages.length - 1;
  const hasMultiplePages = pages.length > 1;
  const formProcessingLabel =
    formProcessingStatus === "switching"
      ? t("form.runner.switchingForm")
      : formProcessingStatus === "clearing"
        ? t("form.runner.clearing")
        : "";

  const output = {
    formDefinitionId: selectedFormId,
    ...(selectedWorkflow ? { processDefinitionVersionId: selectedWorkflow.processDefinitionVersionId } : {}),
    formData: activeFormDefinition ? prepareFormData(activeFormDefinition, values) : values,
  };

  useEffect(() => {
    selectedFormIdRef.current = selectedFormId;
  }, [selectedFormId]);

  useEffect(
    () => () => {
      if (formProcessingTimeoutRef.current !== null) clearTimeout(formProcessingTimeoutRef.current);
    },
    [],
  );

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
        const [result, runnableWorkflows] = await Promise.all([
          api.listForms(token),
          api.listRunnableProcessDefinitions(token).catch(() => []),
        ]);
        if (ignore) {
          return;
        }

        formRunnerFormsCache = result;
        formRunnerWorkflowsCache = runnableWorkflows;
        const currentSelection = result.find((form) => form.id === selectedFormIdRef.current);
        const nextSelectedForm = currentSelection ?? result[0];
        setForms(result);
        setWorkflows(runnableWorkflows);
        setSelectedFormId(nextSelectedForm?.id ?? "");
        const nextFormVersionId = nextSelectedForm
          ? versionAdapter?.resolveVersion?.(nextSelectedForm)?.layout.versionId
            ?? nextSelectedForm.latestPublishedVersionId
          : null;
        setSelectedWorkflowVersionId(
          runnableWorkflows.find((workflow) => workflow.formDefinitionVersionId === nextFormVersionId)
            ?.processDefinitionVersionId ?? "",
        );
        setValues((current) =>
          currentSelection && Object.keys(current).length > 0
            ? current
            : nextSelectedForm
              ? buildInitialValues(resolveRunnerForm(nextSelectedForm, versionAdapter))
              : {},
        );
        setErrors({});
        setSubmitResult(null);
        setActivePageIndex(0);
        setFurthestPageIndex(0);
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
  }, [token, language, t, versionAdapter]);

  function handleChange(fieldKey: string, value: FormValue) {
    setValues((current) => ({ ...current, [fieldKey]: value }));
    setErrors((current) => {
      if (!(fieldKey in current)) {
        return current;
      }

      const next = { ...current };
      delete next[fieldKey];
      return next;
    });
    setSubmitStatus("idle");
    setSubmitResult(null);
  }

  function validateActivePage() {
    if (!activePage) {
      return false;
    }

    const nextErrors = validateFormFields(activePage.fields, values, language);
    setErrors((current) => replacePageErrors(current, activePage.fields.map((field) => field.key), nextErrors));
    if (Object.keys(nextErrors).length > 0) {
      setSubmitStatus("idle");
      setMessage(pagingCopy.pageErrors);
      return false;
    }

    return true;
  }

  function goToPage(pageIndex: number) {
    if (pageIndex === activePageIndex || pageIndex < 0 || pageIndex >= pages.length) {
      return;
    }

    if (pageIndex > activePageIndex && !validateActivePage()) {
      return;
    }

    setActivePageIndex(pageIndex);
    setFurthestPageIndex((current) => Math.max(current, pageIndex));
    setMessage(
      formatPagingCopy(pagingCopy.pageProgress, {
        current: pageIndex + 1,
        total: pages.length,
      }),
    );
  }

  function showNextPage() {
    goToPage(activePageIndex + 1);
  }

  function showPreviousPage() {
    goToPage(activePageIndex - 1);
  }

  function startFormProcessing(status: Exclude<FormProcessingStatus, null>) {
    if (formProcessingTimeoutRef.current !== null) clearTimeout(formProcessingTimeoutRef.current);
    setFormProcessingStatus(status);
    formProcessingTimeoutRef.current = setTimeout(() => {
      setFormProcessingStatus(null);
      formProcessingTimeoutRef.current = null;
    }, FORM_PROCESSING_DURATION_MS);
  }

  function handleFormSelection(nextFormId: string) {
    if (formProcessingTimeoutRef.current !== null || submitInFlightRef.current) return;

    const nextForm = forms.find((form) => form.id === nextFormId);
    startFormProcessing("switching");
    setSelectedFormId(nextFormId);
    setValues(nextForm ? buildInitialValues(resolveRunnerForm(nextForm, versionAdapter)) : {});
    const nextVersionId = nextForm
      ? versionAdapter?.resolveVersion?.(nextForm)?.layout.versionId ?? nextForm.latestPublishedVersionId
      : null;
    setSelectedWorkflowVersionId(
      workflows.find((workflow) => workflow.formDefinitionVersionId === nextVersionId)?.processDefinitionVersionId ?? "",
    );
    setErrors({});
    setSubmitStatus("idle");
    setSubmitResult(null);
    setActivePageIndex(0);
    setFurthestPageIndex(0);
    setMessage(nextForm ? t("form.runner.selectedMessage", { name: nextForm.name }) : t("form.runner.noSavedForm"));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedForm || !activeFormDefinition || !token || !isRunnerReady || isFormProcessing || submitInFlightRef.current) {
      return;
    }

    if (!isLastPage) {
      showNextPage();
      return;
    }

    if (isUnavailableVersion) {
      setMessage(unavailableVersionMessage);
      return;
    }

    const nextErrors = validateFormValues(activeFormDefinition, values, language);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      const firstInvalidPageIndex = pages.findIndex((page) =>
        page.fields.some((field) => Object.hasOwn(nextErrors, field.key)),
      );
      if (firstInvalidPageIndex >= 0) {
        setActivePageIndex(firstInvalidPageIndex);
        setFurthestPageIndex((current) => Math.max(current, firstInvalidPageIndex));
      }
      setSubmitStatus("idle");
      setMessage(t("form.runner.fixFields"));
      return;
    }

    submitInFlightRef.current = true;
    try {
      setSubmitStatus("submitting");
      setSubmitResult(null);
      const formData = prepareFormData(activeFormDefinition, values);
      const process = selectedWorkflow
        ? await api.startProcessVersion(token, selectedWorkflow.processDefinitionVersionId, formData)
        : await api.startProcess(token, { formDefinitionId: selectedForm.id, formData });
      setSubmitStatus("success");
      setSubmitResult(process);
      setMessage(t("form.runner.started", { id: process.id }));
      router.push(`/processes?processId=${encodeURIComponent(process.id)}&started=1`);
    } catch (error) {
      setSubmitStatus("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.runner.startFailed"));
    } finally {
      submitInFlightRef.current = false;
    }
  }

  function resetForm() {
    if (!activeFormDefinition || !isRunnerReady || submitInFlightRef.current || formProcessingTimeoutRef.current !== null) {
      return;
    }

    startFormProcessing("clearing");
    setValues(buildInitialValues(activeFormDefinition));
    setErrors({});
    setSubmitStatus("idle");
    setSubmitResult(null);
    setActivePageIndex(0);
    setFurthestPageIndex(0);
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
        <form className="runner-form" aria-busy={isFormProcessing || submitStatus === "submitting"} onSubmit={handleSubmit}>
          {formProcessingStatus ? (
            <div className="runner-form-processing-overlay" role="status" aria-live="polite">
              <span className="runner-form-processing-indicator">
                <span className="button-spinner" aria-hidden="true" />
                {formProcessingLabel}
              </span>
            </div>
          ) : null}
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
              disabled={loadStatus !== "idle" || !hasForms || submitStatus === "submitting" || isFormProcessing}
              value={selectedFormId}
              onChange={(event) => handleFormSelection(event.target.value)}
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
            <label>
              {t("form.runner.workflow")}
              <select
                disabled={loadStatus !== "idle" || submitStatus === "submitting"}
                onChange={(event) => setSelectedWorkflowVersionId(event.target.value)}
                value={selectedWorkflowVersionId}
              >
                <option value="">{t("form.runner.legacyWorkflow")}</option>
                {availableWorkflows.map((workflow) => (
                  <option key={workflow.processDefinitionVersionId} value={workflow.processDefinitionVersionId}>
                    {t("form.runner.workflowVersion", { name: workflow.name, version: workflow.versionNumber })}
                  </option>
                ))}
              </select>
              {availableWorkflows.length === 0 ? <small>{t("form.runner.noPublishedWorkflow")}</small> : null}
            </label>
          ) : null}

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
              {resolvedPages ? (
                <div className="runner-version-summary">
                  <span>{pagingCopy.version} {resolvedPages.layout.version}</span>
                  <strong className={`form-version-status form-version-status-${resolvedPages.layout.status}`}>
                    {getRunnerVersionStatusLabel(pagingCopy, resolvedPages.layout.status)}
                  </strong>
                </div>
              ) : null}
            </div>
          ) : null}

          {isUnavailableVersion ? (
            <div className="runner-state-panel runner-state-warning" role="status">
              <strong>{versionStatus === "archived" ? pagingCopy.archived : pagingCopy.draft}</strong>
              <span>{unavailableVersionMessage}</span>
            </div>
          ) : null}

          {loadStatus !== "loading" && !selectedForm ? <p className="empty-state">{t("form.runner.noFormPrompt")}</p> : null}

          {hasMultiplePages && activePage ? (
            <nav className="runner-stepper" aria-label={pagingCopy.stepperLabel}>
              <ol>
                {pages.map((page, index) => {
                  const isActive = index === activePageIndex;
                  const isComplete = index < activePageIndex || index < furthestPageIndex;
                  return (
                    <li key={page.id}>
                      <button
                        className={`runner-step${isActive ? " runner-step-active" : ""}${
                          isComplete ? " runner-step-complete" : ""
                        }`}
                        disabled={isNavigationDisabled || index > furthestPageIndex + 1}
                        type="button"
                        aria-current={isActive ? "step" : undefined}
                        aria-label={`${pagingCopy.goToPage}: ${page.title}`}
                        onClick={() => goToPage(index)}
                      >
                        <span className="runner-step-index">{index + 1}</span>
                        <span className="runner-step-copy">
                          <small>{pagingCopy.page} {index + 1}</small>
                          <strong>{page.title}</strong>
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ol>
              <div className="runner-current-page-heading">
                <span>{formatPagingCopy(pagingCopy.pageProgress, { current: activePageIndex + 1, total: pages.length })}</span>
                <h3>{activePage.title}</h3>
                {activePage.description ? <p>{activePage.description}</p> : null}
              </div>
            </nav>
          ) : null}

          {loadStatus !== "loading" && activePage ? (
            <div className="runner-page-fields" key={activePage.id}>
              {activeFields.map((field) => (
                <FieldRenderer
                  key={field.key}
                  field={field}
                  value={values[field.key]}
                  error={errors[field.key]}
                  language={language}
                  onChange={handleChange}
                />
              ))}
            </div>
          ) : null}

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
            {hasMultiplePages && activePageIndex > 0 ? (
              <button className="secondary-button" disabled={isNavigationDisabled} type="button" onClick={showPreviousPage}>
                <ChevronLeft size={18} />
                {pagingCopy.previous}
              </button>
            ) : null}
            {isLastPage ? (
              <button className="primary-button" disabled={isSubmitDisabled} type="submit">
                {submitStatus === "submitting" ? <span className="button-spinner" aria-hidden="true" /> : <Play size={18} />}
                {submitStatus === "submitting" ? t("form.runner.starting") : t("form.runner.startProcess")}
              </button>
            ) : (
              <button className="primary-button" disabled={isNavigationDisabled} type="button" onClick={showNextPage}>
                {pagingCopy.next}
                <ChevronRight size={18} />
              </button>
            )}
            <button
              className="secondary-button"
              aria-busy={formProcessingStatus === "clearing"}
              disabled={isNavigationDisabled}
              type="button"
              onClick={resetForm}
            >
              {formProcessingStatus === "clearing" ? <span className="button-spinner" aria-hidden="true" /> : <RotateCcw size={18} />}
              {formProcessingStatus === "clearing" ? t("form.runner.clearing") : t("form.runner.clear")}
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

function replacePageErrors(
  currentErrors: Record<string, string>,
  pageFieldKeys: string[],
  pageErrors: Record<string, string>,
) {
  const nextErrors = { ...currentErrors };
  for (const fieldKey of pageFieldKeys) {
    delete nextErrors[fieldKey];
  }

  return { ...nextErrors, ...pageErrors };
}

function resolveRunnerForm(
  form: FormDefinition,
  versionAdapter: Pick<FormVersionAdapter, "resolveVersion"> | undefined,
) {
  const version = versionAdapter?.resolveVersion?.(form);
  return version ? { ...form, fields: version.fields } : form;
}

function getRunnerVersionStatusLabel(copy: ReturnType<typeof getFormPagingCopy>, status: FormVersionStatus) {
  if (status === "published") {
    return copy.published;
  }

  return status === "archived" ? copy.archived : copy.draft;
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
