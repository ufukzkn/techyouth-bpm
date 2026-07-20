"use client";

import { ChevronDown, ChevronUp, GripVertical, Plus, Trash2 } from "lucide-react";
import { ExpectedValueInput, SortableFieldCard } from "@/features/form-designer/FormDesignerComponents";
import {
  getDependencyCandidates,
  type DesignerField,
  type DesignerFieldErrors,
  type DesignerPage,
} from "@/features/form-designer/formDesignerModel";
import {
  fieldTypeLabel,
  fieldTypeUsesOptions,
  supportedFieldTypes,
} from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { FieldType, Language, ValidationRule } from "@/lib/types";

export type DesignerSaveFieldError = DesignerFieldErrors[string] & {
  source: "client" | "api";
};

type DirectionFeedback = { id: string; direction: -1 | 1 } | null;

type FormFieldEditorProps = {
  field: DesignerField;
  index: number;
  activePageId: string;
  activeFieldCount: number;
  fields: DesignerField[];
  pages: DesignerPage[];
  language: Language;
  liveFieldError?: DesignerFieldErrors[string];
  saveFieldError?: DesignerSaveFieldError;
  highlighted: boolean;
  moveFeedback: DirectionFeedback;
  displacedFeedback: DirectionFeedback;
  onMoveField: (fieldId: string, direction: -1 | 1) => void;
  onRemoveField: (fieldId: string) => void;
  onMoveFieldToPage: (fieldId: string, pageId: string) => void;
  onUpdateField: (fieldId: string, patch: Partial<Omit<DesignerField, "id">>) => void;
  onUpdateFieldType: (fieldId: string, type: FieldType) => void;
  onToggleRequired: (fieldId: string) => void;
  onAddOption: (fieldId: string) => void;
  onUpdateOption: (fieldId: string, optionIndex: number, value: string) => void;
  onRemoveOption: (fieldId: string, optionIndex: number) => void;
  onAddRule: (fieldId: string) => void;
  onUpdateRule: (fieldId: string, ruleIndex: number, patch: Partial<ValidationRule>) => void;
  onUpdateRuleDependency: (fieldId: string, ruleIndex: number, dependencyKey: string) => void;
  onRemoveRule: (fieldId: string, ruleIndex: number) => void;
};

export function FormFieldEditor({
  field,
  index,
  activePageId,
  activeFieldCount,
  fields,
  pages,
  language,
  liveFieldError,
  saveFieldError,
  highlighted,
  moveFeedback,
  displacedFeedback,
  onMoveField,
  onRemoveField,
  onMoveFieldToPage,
  onUpdateField,
  onUpdateFieldType,
  onToggleRequired,
  onAddOption,
  onUpdateOption,
  onRemoveOption,
  onAddRule,
  onUpdateRule,
  onUpdateRuleDependency,
  onRemoveRule,
}: FormFieldEditorProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) =>
    translate(language, key, values);
  const pagingCopy = getFormPagingCopy(language);
  const isApiSaveError = saveFieldError?.source === "api";
  const keyErrorMessage =
    liveFieldError?.key ?? (isApiSaveError ? saveFieldError?.key : undefined);
  const labelErrorMessage =
    liveFieldError?.label ?? (isApiSaveError ? saveFieldError?.label : undefined);
  const optionsErrorMessage =
    liveFieldError?.options ?? (isApiSaveError ? saveFieldError?.options : undefined);
  const hasKeyError = Boolean(keyErrorMessage);
  const hasLabelError = Boolean(labelErrorMessage);
  const hasOptionsError = Boolean(optionsErrorMessage);
  const optionErrorIndexes = getInvalidOptionIndexes(field, hasOptionsError);
  const showOptionEditorError = hasOptionsError && optionErrorIndexes.size === 0;

  return (
    <SortableFieldCard id={field.id} pageId={activePageId}>
      {({ attributes, listeners, setActivatorNodeRef, isDragging }) => (
        <article
          className={`field-card field-editor${isDragging ? " field-editor-dragging" : ""}${
            highlighted ? " field-editor-highlighted" : ""
          }${
            moveFeedback?.id === field.id
              ? moveFeedback.direction === -1
                ? " field-editor-move-up"
                : " field-editor-move-down"
              : ""
          }${
            displacedFeedback?.id === field.id
              ? displacedFeedback.direction === -1
                ? " field-editor-displaced-up"
                : " field-editor-displaced-down"
              : ""
          }`}
          id={`designer-field-${field.id}`}
        >
          <div className="field-editor-header">
            <div>
              <strong>{field.label || t("form.designer.untitledField")}</strong>
              <span>
                {field.key || t("form.designer.noKey")} - {fieldTypeLabel(language, field.type)} -{" "}
                {t("form.designer.order", { sortOrder: index + 1 })}
              </span>
            </div>
            <div className="field-editor-actions">
              <button
                className="drag-handle"
                type="button"
                ref={setActivatorNodeRef}
                aria-label={t("form.designer.dragHandleAria", { label: field.label || field.key })}
                {...attributes}
                {...listeners}
              >
                <GripVertical size={17} />
                <span>{t("form.designer.dragHandleLabel")}</span>
              </button>
              <button
                className="icon-button"
                disabled={index === 0}
                onClick={() => onMoveField(field.id, -1)}
                type="button"
                aria-label={t("form.designer.moveUp", { label: field.label || field.key })}
              >
                <ChevronUp size={17} />
              </button>
              <button
                className="icon-button"
                disabled={index === activeFieldCount - 1}
                onClick={() => onMoveField(field.id, 1)}
                type="button"
                aria-label={t("form.designer.moveDown", { label: field.label || field.key })}
              >
                <ChevronDown size={17} />
              </button>
              <button
                className="icon-button"
                onClick={() => onRemoveField(field.id)}
                type="button"
                aria-label={t("form.designer.deleteField", { label: field.label || field.key })}
              >
                <Trash2 size={17} />
              </button>
            </div>
          </div>

          <div className="field-editor-grid">
            <label>
              {pagingCopy.fieldPage}
              <select value={activePageId} onChange={(event) => onMoveFieldToPage(field.id, event.target.value)}>
                {pages.map((page, pageIndex) => (
                  <option key={page.id} value={page.id}>
                    {pageIndex + 1}. {page.title || `${pagingCopy.page} ${pageIndex + 1}`}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Key
              <input
                aria-invalid={hasKeyError}
                className={hasKeyError ? "field-editor-control-error field-editor-control-save-error" : undefined}
                value={field.key}
                onChange={(event) => onUpdateField(field.id, { key: event.target.value })}
              />
              {keyErrorMessage ? <span className="field-error field-editor-inline-error">{keyErrorMessage}</span> : null}
            </label>
            <label>
              {t("form.designer.label")}
              <input
                aria-invalid={hasLabelError}
                className={hasLabelError ? "field-editor-control-error field-editor-control-save-error" : undefined}
                value={field.label}
                onChange={(event) => onUpdateField(field.id, { label: event.target.value })}
              />
              {labelErrorMessage ? <span className="field-error field-editor-inline-error">{labelErrorMessage}</span> : null}
            </label>
            <label>
              {t("form.designer.type")}
              <select
                value={field.type}
                onChange={(event) => onUpdateFieldType(field.id, event.target.value as FieldType)}
              >
                {supportedFieldTypes.map((fieldType) => (
                  <option key={fieldType} value={fieldType}>
                    {fieldTypeLabel(language, fieldType)}
                  </option>
                ))}
              </select>
            </label>
            <label className="checkbox-row field-required-toggle">
              <input checked={field.required} onChange={() => onToggleRequired(field.id)} type="checkbox" />
              {t("form.designer.required")}
            </label>
          </div>

          {fieldTypeUsesOptions(field.type) ? (
            <div className={`option-editor${showOptionEditorError ? " option-editor-error option-editor-save-error" : ""}`}>
              <div className="option-editor-header">
                <strong>{t("form.designer.options")}</strong>
                <button className="secondary-button" type="button" onClick={() => onAddOption(field.id)}>
                  <Plus size={16} />
                  {t("form.designer.addOption")}
                </button>
              </div>
              {optionsErrorMessage ? (
                <span className="field-error field-editor-inline-error">{optionsErrorMessage}</span>
              ) : null}
              <div className="option-list">
                {field.options.map((option, optionIndex) => {
                  const hasOptionError = optionErrorIndexes.has(optionIndex);
                  return (
                    <div className="option-row" key={`${field.id}-${optionIndex}`}>
                      <label>
                        {t("form.designer.optionLabel")}
                        <input
                          aria-invalid={hasOptionError}
                          className={hasOptionError ? "field-editor-control-error field-editor-control-save-error" : undefined}
                          value={option}
                          onChange={(event) => onUpdateOption(field.id, optionIndex, event.target.value)}
                        />
                      </label>
                      <button
                        className="icon-button"
                        type="button"
                        onClick={() => onRemoveOption(field.id, optionIndex)}
                        aria-label={t("form.designer.deleteOption", { label: option || t("form.designer.options") })}
                      >
                        <Trash2 size={16} />
                      </button>
                    </div>
                  );
                })}
              </div>
            </div>
          ) : null}

          {field.type === "FileUpload" ? (
            <div className="file-upload-policy-note">
              <strong>{t("form.fileUpload.policyTitle")}</strong>
              <span>{t("form.fileUpload.policyDescription")}</span>
              <span>{t("form.fileUpload.metadataNote")}</span>
            </div>
          ) : null}

          <div className="rule-editor">
            <div className="rule-editor-header">
              <strong>{t("form.designer.dependentValidation")}</strong>
              <button
                className="secondary-button"
                type="button"
                disabled={getDependencyCandidates(fields, field).length === 0}
                onClick={() => onAddRule(field.id)}
              >
                <Plus size={16} />
                {t("form.designer.addRule")}
              </button>
            </div>
            {field.validationRules.length === 0 ? (
              <p className="empty-state">{t("form.designer.noDependentRule")}</p>
            ) : null}
            <div className="rule-list">
              {field.validationRules.map((rule, ruleIndex) => {
                const dependency = fields.find((candidate) => candidate.key.trim() === rule.dependsOnFieldKey);
                const candidates = getDependencyCandidates(fields, field);
                const liveRuleError = liveFieldError?.rules?.[ruleIndex];
                const saveRuleErrorMessage = saveFieldError?.rules?.[ruleIndex];
                const ruleErrorMessage =
                  liveRuleError ?? (isApiSaveError ? saveRuleErrorMessage : undefined);
                const hasRuleError = Boolean(ruleErrorMessage);

                return (
                  <div
                    className={`rule-row${hasRuleError ? " field-editor-rule-error field-editor-rule-save-error" : ""}`}
                    key={`${field.id}-rule-${ruleIndex}`}
                  >
                    <label>
                      {t("form.designer.dependencyField")}
                      <select
                        aria-invalid={hasRuleError}
                        className={hasRuleError ? "field-editor-control-error field-editor-control-save-error" : undefined}
                        value={rule.dependsOnFieldKey}
                        onChange={(event) => onUpdateRuleDependency(field.id, ruleIndex, event.target.value)}
                      >
                        <option value="">{t("form.designer.selectField")}</option>
                        {candidates.map((candidate) => (
                          <option key={candidate.id} value={candidate.key.trim()}>
                            {candidate.label || candidate.key}
                          </option>
                        ))}
                      </select>
                    </label>
                    <ExpectedValueInput
                      dependency={dependency}
                      expectedValue={rule.expectedValue}
                      language={language}
                      onChange={(expectedValue) => onUpdateRule(field.id, ruleIndex, { expectedValue })}
                    />
                    <label>
                      {t("form.designer.message")}
                      <input
                        aria-invalid={hasRuleError}
                        className={hasRuleError ? "field-editor-control-error field-editor-control-save-error" : undefined}
                        value={rule.message}
                        onChange={(event) => onUpdateRule(field.id, ruleIndex, { message: event.target.value })}
                      />
                    </label>
                    <button
                      className="icon-button"
                      type="button"
                      onClick={() => onRemoveRule(field.id, ruleIndex)}
                      aria-label={t("form.designer.deleteRule")}
                    >
                      <Trash2 size={16} />
                    </button>
                    {ruleErrorMessage ? (
                      <span className="field-error rule-error field-editor-inline-error">{ruleErrorMessage}</span>
                    ) : null}
                  </div>
                );
              })}
            </div>
          </div>
        </article>
      )}
    </SortableFieldCard>
  );
}

function getInvalidOptionIndexes(field: DesignerField, hasOptionsError: boolean) {
  if (!hasOptionsError) {
    return new Set<number>();
  }

  const duplicateValues = field.options
    .map((option) => option.trim().toLocaleLowerCase("tr"))
    .filter((option, index, options) => option.length > 0 && options.indexOf(option) !== index);
  return new Set(
    field.options.flatMap((option, optionIndex) => {
      const normalized = option.trim().toLocaleLowerCase("tr");
      return !normalized || duplicateValues.includes(normalized) ? [optionIndex] : [];
    }),
  );
}
