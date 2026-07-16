"use client";

import { ArrowUpRight, CheckCheck, CheckCircle2, Undo2, X, XCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { FieldRenderer } from "@/features/forms/fieldRenderer";
import { validateFormFields } from "@/features/forms/formValidation";
import type { FormValue, FormValues } from "@/features/forms/formValues";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { FormDefinitionVersion, FormFieldDefinition, Language, WorkflowAction } from "@/lib/types";

type TaskActionDialogProps = {
  action: Exclude<WorkflowAction, "Start">;
  language: Language;
  onConfirm: (note: string, formData?: Record<string, unknown>) => void;
  onCancel: () => void;
  disabled: boolean;
  taskForm?: FormDefinitionVersion | null;
};

export function TaskActionDialog({ action, language, onConfirm, onCancel, disabled, taskForm }: TaskActionDialogProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [note, setNote] = useState("");
  const taskFields = useMemo(
    () => taskForm?.pages.flatMap((page) => page.fields).sort((left, right) => left.sortOrder - right.sortOrder) ?? [],
    [taskForm],
  );
  const [formValues, setFormValues] = useState<FormValues>(() => buildTaskFormInitialValues(taskFields));
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const copy = {
    Approve: {
      eyebrow: "dialog.approveAction",
      title: "dialog.approveTitle",
      placeholder: "dialog.approvePlaceholder",
      defaultNote: "dialog.defaultApproveNote",
      buttonClass: "success-button",
      icon: CheckCircle2,
    },
    Reject: {
      eyebrow: "dialog.rejectAction",
      title: "dialog.rejectTitle",
      placeholder: "dialog.rejectPlaceholder",
      defaultNote: "dialog.defaultRejectNote",
      buttonClass: "danger-button",
      icon: XCircle,
    },
    Escalate: {
      eyebrow: "dialog.escalateAction",
      title: "dialog.escalateTitle",
      placeholder: "dialog.escalatePlaceholder",
      defaultNote: "dialog.defaultEscalateNote",
      buttonClass: "escalate-button",
      icon: ArrowUpRight,
    },
    SendBack: {
      eyebrow: "dialog.sendBackAction",
      title: "dialog.sendBackTitle",
      placeholder: "dialog.sendBackPlaceholder",
      defaultNote: "dialog.defaultSendBackNote",
      buttonClass: "secondary-button",
      icon: Undo2,
    },
    Complete: {
      eyebrow: "dialog.completeAction",
      title: "dialog.completeTitle",
      placeholder: "dialog.completePlaceholder",
      defaultNote: "dialog.defaultCompleteNote",
      buttonClass: "success-button",
      icon: CheckCheck,
    },
  } satisfies Record<Exclude<WorkflowAction, "Start">, {
    eyebrow: TranslationKey;
    title: TranslationKey;
    placeholder: TranslationKey;
    defaultNote: TranslationKey;
    buttonClass: string;
    icon: typeof CheckCircle2;
  }>;
  const actionCopy = copy[action];
  const ActionIcon = actionCopy.icon;

  function updateFormValue(fieldKey: string, value: FormValue) {
    setFormValues((current) => ({ ...current, [fieldKey]: value }));
    setFormErrors((current) => {
      if (!current[fieldKey]) return current;
      const next = { ...current };
      delete next[fieldKey];
      return next;
    });
  }

  function confirmAction() {
    const nextErrors = validateFormFields(taskFields, formValues, language);
    setFormErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    onConfirm(
      note || t(actionCopy.defaultNote),
      taskForm ? prepareTaskFormData(taskFields, formValues) : undefined,
    );
  }

  return (
    <div className="action-dialog-overlay" onClick={onCancel}>
      <div className={`action-dialog${taskForm ? " action-dialog-with-form" : ""}`} onClick={(e) => e.stopPropagation()}>
        <div className="action-dialog-header">
          <div>
            <span className="eyebrow">
              {t(actionCopy.eyebrow)}
            </span>
            <strong>{t(actionCopy.title)}</strong>
          </div>
          <button className="icon-button" onClick={onCancel} type="button" aria-label={t("common.close")}>
            <X size={18} />
          </button>
        </div>

        {taskForm ? (
          <section className="action-dialog-task-form" aria-label={taskForm.formName}>
            <div className="action-dialog-task-form-heading">
              <span className="eyebrow">{language === "tr" ? "Adim formu" : "Step form"}</span>
              <strong>{taskForm.formName}</strong>
            </div>
            {taskForm.pages.map((page) => (
              <div className="action-dialog-task-page" key={page.id}>
                {taskForm.pages.length > 1 ? <h3>{page.title}</h3> : null}
                {page.description ? <p>{page.description}</p> : null}
                <div className="action-dialog-task-fields">
                  {page.fields
                    .slice()
                    .sort((left, right) => left.sortOrder - right.sortOrder)
                    .map((field) => (
                      <FieldRenderer
                        error={formErrors[field.key]}
                        field={field}
                        key={field.key}
                        language={language}
                        onChange={updateFormValue}
                        value={formValues[field.key]}
                      />
                    ))}
                </div>
              </div>
            ))}
          </section>
        ) : null}

        <label className="action-dialog-label">
          {t("dialog.actionNote")}
          <textarea
            className="action-dialog-textarea"
            placeholder={t(actionCopy.placeholder)}
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>

        <div className="action-dialog-actions">
          <button className="secondary-button" onClick={onCancel} type="button" disabled={disabled}>
            {t("common.cancel")}
          </button>
          <button
            className={actionCopy.buttonClass}
            disabled={disabled}
            onClick={confirmAction}
            type="button"
          >
            <ActionIcon size={17} />
            {disabled ? t("common.saving") : translate(language, `action.${action}` as TranslationKey)}
          </button>
        </div>
      </div>
    </div>
  );
}

function buildTaskFormInitialValues(fields: FormFieldDefinition[]) {
  return fields.reduce<FormValues>((current, field) => {
    current[field.key] = field.type === "Checkbox" ? false : "";
    return current;
  }, {});
}

function prepareTaskFormData(fields: FormFieldDefinition[], values: FormValues) {
  return fields.reduce<Record<string, unknown>>((current, field) => {
    const value = values[field.key];
    current[field.key] = field.type === "Number" && value !== "" ? Number(value) : value;
    return current;
  }, {});
}
