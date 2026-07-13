"use client";

import {
  AlignLeft,
  Calendar,
  ChevronDown,
  ChevronUp,
  CircleDot,
  GripVertical,
  Hash,
  List,
  Mail,
  Plus,
  Save,
  SquareCheck,
  Trash2,
  Type,
  type LucideIcon,
} from "lucide-react";
import { type ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import {
  closestCenter,
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { MobileFieldPalette } from "@/features/form-designer/MobileFieldPalette";
import { JsonViewer } from "@/features/ui/JsonViewer";
import {
  createDefaultField,
  createDefaultOptions,
  createFieldKey,
  fieldTypeLabel,
  fieldTypeUsesOptions,
  supportedFieldTypes,
} from "@/features/forms/fieldTypes";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { CreateFormRequest, FieldType, FormDefinition, FormFieldDefinition, Language, ValidationRule } from "@/lib/types";

type DesignerField = Omit<FormFieldDefinition, "id"> & {
  id: string;
};

const fieldPalettePrefix = "palette:";
const fieldCanvasDropId = "field-canvas";
const paletteDragDistanceThreshold = 8;
const fieldTypeIcons: Record<FieldType, LucideIcon> = {
  Text: Type,
  TextArea: AlignLeft,
  Number: Hash,
  Email: Mail,
  Select: List,
  Radio: CircleDot,
  Checkbox: SquareCheck,
  Date: Calendar,
};

const initialFields: DesignerField[] = [
  {
    id: "customerName",
    key: "customerName",
    label: "Müşteri adı",
    type: "Text",
    required: true,
    sortOrder: 1,
    options: [],
    validationRules: [],
  },
  {
    id: "requestType",
    key: "requestType",
    label: "Talep tipi",
    type: "Select",
    required: true,
    sortOrder: 2,
    options: ["İzin", "Masraf", "Satın Alma"],
    validationRules: [],
  },
  {
    id: "approvalNote",
    key: "approvalNote",
    label: "Onay açıklaması",
    type: "Text",
    required: false,
    sortOrder: 3,
    options: [],
    validationRules: [
      {
        ruleType: "RequiredWhen",
        dependsOnFieldKey: "requestType",
        expectedValue: "Satın Alma",
        message: "Satın Alma taleplerinde onay açıklaması zorunludur.",
      },
    ],
  },
];

export function FormDesignerDraft() {
  const token = useSessionStore((state) => state.token);
  const language = useSessionStore((state) => state.language);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [fields, setFields] = useState<DesignerField[]>(initialFields);
  const [formName, setFormName] = useState("Demo Süreç Formu");
  const [description, setDescription] = useState("Frontend tarafında tasarlanan form modeli");
  const [savedForms, setSavedForms] = useState<FormDefinition[]>([]);
  const [selectedFormId, setSelectedFormId] = useState("");
  const [isLoadingForms, setIsLoadingForms] = useState(false);
  const [label, setLabel] = useState("Masraf merkezi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);
  const [saveState, setSaveState] = useState<"idle" | "saving" | "success" | "error">("idle");
  const [message, setMessage] = useState(() => t("form.designer.notSaved"));
  const [highlightedFieldId, setHighlightedFieldId] = useState("");
  const [paletteInsertIndex, setPaletteInsertIndex] = useState<number | null>(null);
  const fieldErrors = useMemo(() => validateDesignerFields(fields, language), [fields, language]);
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;
  const selectedFormName = savedForms.find((form) => form.id === selectedFormId)?.name;
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: paletteDragDistanceThreshold,
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const formModel = useMemo<CreateFormRequest>(
    () => ({
      name: formName,
      description,
      fields: fields.map((field, index) => ({
        key: createDesignerFieldKey(field.key, index + 1),
        label: field.label.trim(),
        type: field.type,
        required: field.required,
        sortOrder: index + 1,
        options: fieldTypeUsesOptions(field.type) ? field.options.map((option) => option.trim()).filter(Boolean) : [],
        validationRules: field.validationRules.map((rule) => ({
          ruleType: rule.ruleType,
          dependsOnFieldKey: createDesignerFieldKey(rule.dependsOnFieldKey, 1),
          expectedValue: rule.expectedValue.trim(),
          message: rule.message.trim(),
        })),
      })),
    }),
    [description, fields, formName],
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    let ignore = false;
    const activeToken = token;

    async function loadForms() {
      try {
        setIsLoadingForms(true);
        const result = await api.listForms(activeToken);
        if (!ignore) {
          setSavedForms(result);
        }
      } catch (error) {
        if (!ignore) {
          setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.designer.loadFailed"));
        }
      } finally {
        if (!ignore) {
          setIsLoadingForms(false);
        }
      }
    }

    loadForms();

    return () => {
      ignore = true;
    };
  }, [token, language, t]);

  useEffect(() => {
    if (!highlightedFieldId) {
      return;
    }

    const timeoutId = window.setTimeout(() => setHighlightedFieldId(""), 1800);
    return () => window.clearTimeout(timeoutId);
  }, [highlightedFieldId]);

  function handleDragStart(event: DragStartEvent) {
    if (isPaletteDragId(event.active.id)) {
      setPaletteInsertIndex(fields.length);
    }
  }

  function handleDragOver(event: DragOverEvent) {
    if (!isPaletteDragId(event.active.id)) {
      return;
    }

    setPaletteInsertIndex(resolvePaletteInsertIndex(event, fields));
  }

  function handleDragCancel(event: DragCancelEvent) {
    if (isPaletteDragId(event.active.id)) {
      setPaletteInsertIndex(null);
    }
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;

    if (!over) {
      setPaletteInsertIndex(null);
      return;
    }

    if (isPaletteDragId(active.id)) {
      const fieldType = active.data.current?.fieldType;
      const insertIndex = resolvePaletteInsertIndex(event, fields);
      if (insertIndex !== null && hasPaletteDragDistance(event.delta) && isSupportedFieldType(fieldType)) {
        addFieldFromPalette(fieldType, insertIndex);
      }
      setPaletteInsertIndex(null);
      return;
    }

    if (active.id === over.id) {
      return;
    }

    setFields((current) => {
      const oldIndex = current.findIndex((field) => field.id === active.id);
      const newIndex = current.findIndex((field) => field.id === over.id);

      if (oldIndex < 0 || newIndex < 0) {
        return current;
      }

      return normalizeSortOrder(arrayMove(current, oldIndex, newIndex));
    });
    markUnsaved();
  }

  function addFieldFromPalette(fieldType: FieldType, insertIndex: number) {
    const defaultLabel = getPaletteFieldDefaultLabel(language, fieldType);
    const addedFieldId = `palette-${fieldType}-${Date.now()}`;

    setFields((current) => {
      const safeInsertIndex = Math.min(Math.max(insertIndex, 0), current.length);
      const field = createDefaultField({
        label: defaultLabel,
        type: fieldType,
        required: false,
        sortOrder: safeInsertIndex + 1,
        language,
      });
      const nextField: DesignerField = {
        ...field,
        id: addedFieldId,
      };
      const nextFields = [...current];
      nextFields.splice(safeInsertIndex, 0, nextField);

      return normalizeSortOrder(nextFields);
    });
    setSaveState("idle");
    setMessage(t("form.designer.fieldAddedFromPalette", { label: defaultLabel }));
    setHighlightedFieldId(addedFieldId);
    window.requestAnimationFrame(() => {
      document.getElementById(`designer-field-${addedFieldId}`)?.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
      });
    });
  }

  function addField() {
    const field = createDefaultField({
      label,
      type,
      required,
      sortOrder: fields.length + 1,
      language,
    });
    const nextField: DesignerField = {
      ...field,
      id: `${field.key}-${Date.now()}`,
    };

    setFields((current) => [...current, nextField]);
    setLabel("");
    setType("Text");
    setRequired(false);
    setSaveState("idle");
    setMessage(t("form.designer.unsaved"));
  }

  function updateField(id: string, patch: Partial<Omit<DesignerField, "id">>) {
    setFields((current) => current.map((field) => (field.id === id ? { ...field, ...patch } : field)));
    markUnsaved();
  }

  function updateFieldType(id: string, nextType: FieldType) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== id) {
          return field;
        }

        return {
          ...field,
          type: nextType,
          options: fieldTypeUsesOptions(nextType)
            ? field.options.length > 0
              ? field.options
              : createDefaultOptions(nextType, language)
            : [],
        };
      }),
    );
    markUnsaved();
  }

  function removeField(id: string) {
    setFields((current) => normalizeSortOrder(current.filter((field) => field.id !== id)));
    markUnsaved();
  }

  function toggleRequired(id: string) {
    setFields((current) =>
      current.map((field) => (field.id === id ? { ...field, required: !field.required } : field)),
    );
    markUnsaved();
  }

  function moveField(id: string, direction: -1 | 1) {
    setFields((current) => {
      const currentIndex = current.findIndex((field) => field.id === id);
      const nextIndex = currentIndex + direction;

      if (currentIndex < 0 || nextIndex < 0 || nextIndex >= current.length) {
        return current;
      }

      const nextFields = [...current];
      const [field] = nextFields.splice(currentIndex, 1);
      nextFields.splice(nextIndex, 0, field);
      return normalizeSortOrder(nextFields);
    });
    markUnsaved();
  }

  function addOption(fieldId: string) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId ? { ...field, options: [...field.options, `Secenek ${field.options.length + 1}`] } : field,
      ),
    );
    markUnsaved();
  }

  function updateOption(fieldId: string, optionIndex: number, value: string) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              options: field.options.map((option, index) => (index === optionIndex ? value : option)),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function removeOption(fieldId: string, optionIndex: number) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              options: field.options.filter((_, index) => index !== optionIndex),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function addRequiredWhenRule(fieldId: string) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = findFirstDependencyField(current, field);
        const rule: ValidationRule = {
          ruleType: "RequiredWhen",
          dependsOnFieldKey: dependency?.key.trim() ?? "",
          expectedValue: dependency ? getDefaultExpectedValue(dependency) : "",
          message: t("form.validation.requiredWhenDefault", { label: field.label || field.key }),
        };

        return { ...field, validationRules: [...field.validationRules, rule] };
      }),
    );
    markUnsaved();
  }

  function updateRequiredWhenRule(fieldId: string, ruleIndex: number, patch: Partial<ValidationRule>) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              validationRules: field.validationRules.map((rule, index) =>
                index === ruleIndex ? { ...rule, ...patch } : rule,
              ),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function updateRuleDependency(fieldId: string, ruleIndex: number, dependsOnFieldKey: string) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = current.find((candidate) => candidate.key.trim() === dependsOnFieldKey);
        return {
          ...field,
          validationRules: field.validationRules.map((rule, index) =>
            index === ruleIndex
              ? {
                  ...rule,
                  dependsOnFieldKey,
                  expectedValue: dependency ? getDefaultExpectedValue(dependency) : "",
                }
              : rule,
          ),
        };
      }),
    );
    markUnsaved();
  }

  function removeRequiredWhenRule(fieldId: string, ruleIndex: number) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              validationRules: field.validationRules.filter((_, index) => index !== ruleIndex),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function markUnsaved() {
    setSaveState("idle");
    setMessage(t("form.designer.unsaved"));
  }

  async function loadSavedForm(id: string) {
    setSelectedFormId(id);

    if (!id) {
      resetDesigner();
      return;
    }

    if (!token) {
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredLoad"));
      return;
    }

    try {
      setIsLoadingForms(true);
      const form = await api.getForm(token, id);
      setSelectedFormId(form.id);
      setFormName(form.name);
      setDescription(form.description);
      setFields(toDesignerFields(form));
      setSaveState("idle");
      setMessage(t("form.designer.loadedForEdit", { name: form.name }));
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.designer.formLoadFailed"));
    } finally {
      setIsLoadingForms(false);
    }
  }

  function resetDesigner() {
    setSelectedFormId("");
    setFormName("Demo Süreç Formu");
    setDescription("Frontend tarafında tasarlanan form modeli");
    setFields(initialFields);
    setSaveState("idle");
    setMessage(t("form.designer.draftReady"));
  }

  async function saveForm() {
    if (!token) {
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredSave"));
      return;
    }

    if (hasFieldErrors) {
      setSaveState("error");
      setMessage(t("form.designer.fixErrorsBeforeSave"));
      return;
    }

    try {
      setSaveState("saving");
      const isUpdate = selectedFormId.length > 0;
      const saved = isUpdate ? await api.updateForm(token, selectedFormId, formModel) : await api.createForm(token, formModel);
      setSelectedFormId(saved.id);
      setSavedForms((current) => upsertForm(current, saved));
      setFields(toDesignerFields(saved));
      setSaveState("success");
      setMessage(
        t("form.designer.savedMessage", {
          action: isUpdate ? t("form.designer.savedActionUpdated") : t("form.designer.savedActionCreated"),
          name: saved.name,
        }),
      );
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : t("form.designer.saveFailed"));
    }
  }

  return (
    <section className="designer-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("form.designer.eyebrow")}</span>
          <h2>{t("form.designer.title")}</h2>
        </div>
        <p>{t("form.designer.description")}</p>
      </div>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragCancel={handleDragCancel}
        onDragEnd={handleDragEnd}
        onDragOver={handleDragOver}
        onDragStart={handleDragStart}
      >
        <div className="designer-grid">
          <div className="tool-panel">
            <h3>{t("form.designer.formInfo")}</h3>
            <label>
              {t("form.designer.savedForm")}
              <select disabled={isLoadingForms} value={selectedFormId} onChange={(event) => loadSavedForm(event.target.value)}>
                <option value="">{isLoadingForms ? t("form.designer.loadingForms") : t("form.designer.newDraft")}</option>
                {savedForms.map((form) => (
                  <option key={form.id} value={form.id}>
                    {form.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("form.designer.formName")}
              <input
                value={formName}
                onChange={(event) => {
                  setFormName(event.target.value);
                  markUnsaved();
                }}
              />
            </label>
            <label>
              {t("form.designer.descriptionLabel")}
              <input
                value={description}
                onChange={(event) => {
                  setDescription(event.target.value);
                  markUnsaved();
                }}
              />
            </label>
            <p className="helper-copy">
              {selectedFormId
                ? t("form.designer.editingSelected", { name: selectedFormName ?? t("form.designer.selectedForm") })
                : t("form.designer.createOnSave")}
            </p>
            <button className="secondary-button" disabled={saveState === "saving"} type="button" onClick={resetDesigner}>
              <Plus size={18} />
              {t("form.designer.newForm")}
            </button>
            <button className="primary-button" disabled={saveState === "saving"} type="button" onClick={saveForm}>
              <Save size={18} />
              {saveState === "saving"
                ? t("form.designer.saving")
                : selectedFormId
                  ? t("form.designer.updateForm")
                  : t("form.designer.saveForm")}
            </button>
            {hasFieldErrors ? <p className="field-error">{t("form.designer.blockingErrors")}</p> : null}
            <p className={`status-line status-line-${saveState}`} aria-live="polite">
              {message}
            </p>
            <ol className="demo-steps" aria-label={t("form.designer.demoStepsAria")}>
              <li>{t("form.designer.demoStepEdit")}</li>
              <li>{t("form.designer.demoStepOptions")}</li>
              <li>{t("form.designer.demoStepRequiredWhen")}</li>
              <li>{t("form.designer.demoStepOrdering")}</li>
            </ol>
          </div>

          <div className="tool-panel">
            <h3>{t("form.designer.addFieldTitle")}</h3>
            <label>
              {t("form.designer.label")}
              <input
                value={label}
                onChange={(event) => setLabel(event.target.value)}
                placeholder={t("form.designer.labelPlaceholder")}
              />
            </label>
            <label>
              {t("form.designer.type")}
              <select value={type} onChange={(event) => setType(event.target.value as FieldType)}>
                {supportedFieldTypes.map((fieldType) => (
                  <option key={fieldType} value={fieldType}>
                    {fieldTypeLabel(language, fieldType)}
                  </option>
                ))}
              </select>
            </label>
            <label className="checkbox-row">
              <input checked={required} onChange={(event) => setRequired(event.target.checked)} type="checkbox" />
              {t("form.designer.requiredField")}
            </label>
            <button className="secondary-button" type="button" onClick={addField}>
              <Plus size={18} />
              {t("form.designer.addField")}
            </button>
          </div>

          <SortableContext items={fields.map((field) => field.id)} strategy={verticalListSortingStrategy}>
            <FieldCanvasDropZone label={t("form.designer.fieldListAria")}>
              <div className="designer-help-panel">
                <strong>{t("form.designer.dropZoneTitle")}</strong>
                <span>
                  {t("form.designer.fieldListHelpDescription")} {t("form.designer.dropZoneDescription")}
                </span>
              </div>
              {fields.map((field, index) => (
                <SortableFieldCard id={field.id} key={field.id}>
                  {({ attributes, listeners, setActivatorNodeRef, isDragging }) => (
                    <>
                      {paletteInsertIndex === index ? <div className="field-insert-indicator" /> : null}
                      <article
                        className={`field-card field-editor${isDragging ? " field-editor-dragging" : ""}${
                          highlightedFieldId === field.id ? " field-editor-highlighted" : ""
                        }`}
                        id={`designer-field-${field.id}`}
                      >
                      <div className="field-editor-header">
                        <div>
                          <strong>{field.label || t("form.designer.untitledField")}</strong>
                          <span>
                            {field.key || t("form.designer.noKey")} - {fieldTypeLabel(language, field.type)} -{" "}
                            {t("form.designer.order", { sortOrder: field.sortOrder })}
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
                            onClick={() => moveField(field.id, -1)}
                            type="button"
                            aria-label={t("form.designer.moveUp", { label: field.label || field.key })}
                          >
                            <ChevronUp size={17} />
                          </button>
                          <button
                            className="icon-button"
                            disabled={index === fields.length - 1}
                            onClick={() => moveField(field.id, 1)}
                            type="button"
                            aria-label={t("form.designer.moveDown", { label: field.label || field.key })}
                          >
                            <ChevronDown size={17} />
                          </button>
                          <button
                            className="icon-button"
                            onClick={() => removeField(field.id)}
                            type="button"
                            aria-label={t("form.designer.deleteField", { label: field.label || field.key })}
                          >
                            <Trash2 size={17} />
                          </button>
                        </div>
                      </div>

                      <div className="field-editor-grid">
                        <label>
                          Key
                          <input
                            value={field.key}
                            onBlur={(event) => updateField(field.id, { key: createDesignerFieldKey(event.target.value, index + 1) })}
                            onChange={(event) => updateField(field.id, { key: event.target.value })}
                          />
                          {fieldErrors[field.id]?.key ? <span className="field-error">{fieldErrors[field.id]?.key}</span> : null}
                        </label>
                        <label>
                          {t("form.designer.label")}
                          <input value={field.label} onChange={(event) => updateField(field.id, { label: event.target.value })} />
                          {fieldErrors[field.id]?.label ? (
                            <span className="field-error">{fieldErrors[field.id]?.label}</span>
                          ) : null}
                        </label>
                        <label>
                          {t("form.designer.type")}
                          <select value={field.type} onChange={(event) => updateFieldType(field.id, event.target.value as FieldType)}>
                            {supportedFieldTypes.map((fieldType) => (
                              <option key={fieldType} value={fieldType}>
                                {fieldTypeLabel(language, fieldType)}
                              </option>
                            ))}
                          </select>
                        </label>
                        <label className="checkbox-row field-required-toggle">
                          <input checked={field.required} onChange={() => toggleRequired(field.id)} type="checkbox" />
                          {t("form.designer.required")}
                        </label>
                      </div>

                      {fieldTypeUsesOptions(field.type) ? (
                        <div className="option-editor">
                          <div className="option-editor-header">
                            <strong>{t("form.designer.options")}</strong>
                            <button className="secondary-button" type="button" onClick={() => addOption(field.id)}>
                              <Plus size={16} />
                              {t("form.designer.addOption")}
                            </button>
                          </div>
                          {fieldErrors[field.id]?.options ? (
                            <span className="field-error">{fieldErrors[field.id]?.options}</span>
                          ) : null}
                          <div className="option-list">
                            {field.options.map((option, optionIndex) => (
                              <div className="option-row" key={`${field.id}-${optionIndex}`}>
                                <label>
                                  {t("form.designer.optionLabel")}
                                  <input value={option} onChange={(event) => updateOption(field.id, optionIndex, event.target.value)} />
                                </label>
                                <button
                                  className="icon-button"
                                  type="button"
                                  onClick={() => removeOption(field.id, optionIndex)}
                                  aria-label={t("form.designer.deleteOption", { label: option || t("form.designer.options") })}
                                >
                                  <Trash2 size={16} />
                                </button>
                              </div>
                            ))}
                          </div>
                        </div>
                      ) : null}

                      <div className="rule-editor">
                        <div className="rule-editor-header">
                          <div>
                            <strong>{t("form.designer.dependentValidation")}</strong>
                          </div>
                          <button
                            className="secondary-button"
                            type="button"
                            disabled={getDependencyCandidates(fields, field).length === 0}
                            onClick={() => addRequiredWhenRule(field.id)}
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
                            const ruleError = fieldErrors[field.id]?.rules?.[ruleIndex];

                            return (
                              <div className="rule-row" key={`${field.id}-rule-${ruleIndex}`}>
                                <label>
                                  {t("form.designer.dependencyField")}
                                  <select
                                    value={rule.dependsOnFieldKey}
                                    onChange={(event) => updateRuleDependency(field.id, ruleIndex, event.target.value)}
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
                                  onChange={(expectedValue) => updateRequiredWhenRule(field.id, ruleIndex, { expectedValue })}
                                />

                                <label>
                                  {t("form.designer.message")}
                                  <input
                                    value={rule.message}
                                    onChange={(event) => updateRequiredWhenRule(field.id, ruleIndex, { message: event.target.value })}
                                  />
                                </label>

                                <button
                                  className="icon-button"
                                  type="button"
                                  onClick={() => removeRequiredWhenRule(field.id, ruleIndex)}
                                  aria-label={t("form.designer.deleteRule")}
                                >
                                  <Trash2 size={16} />
                                </button>

                                {ruleError ? <span className="field-error rule-error">{ruleError}</span> : null}
                              </div>
                            );
                          })}
                        </div>
                      </div>
                      </article>
                    </>
                  )}
                </SortableFieldCard>
              ))}
              {paletteInsertIndex === fields.length ? <div className="field-insert-indicator" /> : null}
            </FieldCanvasDropZone>
          </SortableContext>

          <div className="json-preview-panel">
            <div>
              <span className="eyebrow">{t("form.designer.jsonPreviewEyebrow")}</span>
              <h3>{t("form.designer.jsonPreviewTitle")}</h3>
              <p>{t("form.designer.jsonPreviewDescription")}</p>
            </div>
            <JsonViewer language={language} value={formModel} />
          </div>

          <aside className="field-palette-rail" aria-label={t("form.designer.fieldPaletteStickyTitle")}>
            <div className="field-palette">
              <div className="field-palette-header">
                <strong>{t("form.designer.fieldPaletteStickyTitle")}</strong>
                <span>{t("form.designer.fieldPaletteStickyDescription")}</span>
              </div>
              <div className="field-palette-grid">
                {supportedFieldTypes.map((fieldType) => (
                  <PaletteFieldTypeCard fieldType={fieldType} key={fieldType} language={language} />
                ))}
              </div>
            </div>
          </aside>
        </div>
      </DndContext>
      <MobileFieldPalette
        closeLabel={t("common.close")}
        description={t("form.designer.mobilePaletteDescription")}
        items={supportedFieldTypes.map((fieldType) => ({
          type: fieldType,
          label: fieldTypeLabel(language, fieldType),
          description: translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey),
          icon: fieldTypeIcons[fieldType],
        }))}
        onSelect={(fieldType) => addFieldFromPalette(fieldType, fields.length)}
        openLabel={t("form.designer.mobilePaletteOpen")}
        title={t("form.designer.fieldPaletteTitle")}
      />
    </section>
  );
}

function PaletteFieldTypeCard({
  fieldType,
  language,
}: {
  fieldType: FieldType;
  language: Language;
}) {
  const label = fieldTypeLabel(language, fieldType);
  const description = translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey);
  const FieldTypeIcon = fieldTypeIcons[fieldType];
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `${fieldPalettePrefix}${fieldType}`,
    data: { fieldType },
  });
  const style = {
    transform: CSS.Translate.toString(transform),
  };

  return (
    <div
      ref={setNodeRef}
      className={`field-palette-item${isDragging ? " field-palette-item-dragging" : ""}`}
      style={style}
      aria-label={translate(language, "form.designer.dragFieldType", { type: label })}
      {...attributes}
      {...listeners}
    >
      <span className="field-palette-icon" aria-hidden="true">
        <FieldTypeIcon size={18} />
      </span>
      <span className="field-palette-item-copy">
        <strong>{label}</strong>
        <span>{description}</span>
      </span>
      <GripVertical className="field-palette-grip" size={16} aria-hidden="true" />
    </div>
  );
}

function FieldCanvasDropZone({ children, label }: { children: ReactNode; label: string }) {
  const { isOver, setNodeRef } = useDroppable({ id: fieldCanvasDropId });

  return (
    <div ref={setNodeRef} className={`field-list${isOver ? " field-list-drop-active" : ""}`} aria-label={label}>
      {children}
    </div>
  );
}

type SortableFieldCardRenderProps = Pick<
  ReturnType<typeof useSortable>,
  "attributes" | "listeners" | "setActivatorNodeRef" | "isDragging"
>;

function SortableFieldCard({
  id,
  children,
}: {
  id: string;
  children: (props: SortableFieldCardRenderProps) => ReactNode;
}) {
  const { attributes, listeners, setActivatorNodeRef, setNodeRef, transform, transition, isDragging } = useSortable({ id });
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style}>
      {children({ attributes, listeners, setActivatorNodeRef, isDragging })}
    </div>
  );
}

function normalizeSortOrder(fields: DesignerField[]) {
  return fields.map((field, index) => ({ ...field, sortOrder: index + 1 }));
}

function createDesignerFieldKey(value: string, fallbackIndex: number) {
  return createFieldKey(value, fallbackIndex);
}

function isPaletteDragId(id: DragEndEvent["active"]["id"]) {
  return String(id).startsWith(fieldPalettePrefix);
}

function hasPaletteDragDistance(delta: DragEndEvent["delta"]) {
  return Math.hypot(delta.x, delta.y) >= paletteDragDistanceThreshold;
}

function resolvePaletteInsertIndex(event: DragOverEvent | DragEndEvent, fields: DesignerField[]) {
  const over = event.over;
  if (!over) {
    return null;
  }

  if (over.id === fieldCanvasDropId) {
    return fields.length;
  }

  const overFieldIndex = fields.findIndex((field) => field.id === over.id);
  if (overFieldIndex < 0) {
    return null;
  }

  const translatedRect = event.active.rect.current.translated;
  if (!translatedRect) {
    return overFieldIndex;
  }

  const activeCenterY = translatedRect.top + translatedRect.height / 2;
  const overMiddleY = over.rect.top + over.rect.height / 2;
  return activeCenterY > overMiddleY ? overFieldIndex + 1 : overFieldIndex;
}

function isSupportedFieldType(value: unknown): value is FieldType {
  return supportedFieldTypes.includes(value as FieldType);
}

function getPaletteFieldDefaultLabel(language: Language, fieldType: FieldType) {
  return translate(language, `form.designer.fieldType${fieldType}Label` as TranslationKey);
}

function toDesignerFields(form: FormDefinition) {
  return normalizeSortOrder(
    form.fields.map((field, index) => ({
      ...field,
      id: field.id ?? `${field.key}-${index}`,
      options: field.options ?? [],
      validationRules: field.validationRules ?? [],
      sortOrder: index + 1,
    })),
  );
}

function upsertForm(forms: FormDefinition[], form: FormDefinition) {
  const exists = forms.some((item) => item.id === form.id);
  if (!exists) {
    return [form, ...forms];
  }

  return forms.map((item) => (item.id === form.id ? form : item));
}

type DesignerFieldErrors = Record<string, { key?: string; label?: string; options?: string; rules?: Record<number, string> }>;

function validateDesignerFields(fields: DesignerField[], language: Language) {
  const errors: DesignerFieldErrors = {};
  const keyCounts = fields.reduce<Record<string, number>>((current, field) => {
    const key = createDesignerFieldKey(field.key, field.sortOrder).toLowerCase();
    if (key) {
      current[key] = (current[key] ?? 0) + 1;
    }

    return current;
  }, {});

  for (const field of fields) {
    const fieldError: DesignerFieldErrors[string] = {};
    const key = createDesignerFieldKey(field.key, field.sortOrder);

    if (!key) {
      fieldError.key = translate(language, "form.validation.fieldKeyRequired");
    } else if (keyCounts[key.toLowerCase()] > 1) {
      fieldError.key = translate(language, "form.validation.fieldKeyUnique");
    }

    if (!field.label.trim()) {
      fieldError.label = translate(language, "form.validation.labelRequired");
    }

    if (fieldTypeUsesOptions(field.type)) {
      const filledOptions = field.options.map((option) => option.trim()).filter(Boolean);
      if (filledOptions.length === 0) {
        fieldError.options = translate(language, "form.validation.optionsRequired");
      } else if (field.options.some((option) => option.trim().length === 0)) {
        fieldError.options = translate(language, "form.validation.optionValueRequired");
      } else if (new Set(filledOptions.map((option) => option.toLocaleLowerCase("tr"))).size !== filledOptions.length) {
        fieldError.options = translate(language, "form.validation.optionValueUnique");
      }
    }

    for (const [ruleIndex, rule] of field.validationRules.entries()) {
      const dependency = fields.find(
        (candidate) => createDesignerFieldKey(candidate.key, candidate.sortOrder) === createDesignerFieldKey(rule.dependsOnFieldKey, 1),
      );
      const ruleError = validateRequiredWhenRule(field, rule, dependency, language);
      if (ruleError) {
        fieldError.rules = { ...fieldError.rules, [ruleIndex]: ruleError };
      }
    }

    if (Object.keys(fieldError).length > 0) {
      errors[field.id] = fieldError;
    }
  }

  return errors;
}

function getDependencyCandidates(fields: DesignerField[], field: DesignerField) {
  return fields.filter((candidate) => candidate.id !== field.id && candidate.key.trim().length > 0);
}

function findFirstDependencyField(fields: DesignerField[], field: DesignerField) {
  return getDependencyCandidates(fields, field)[0];
}

function getDefaultExpectedValue(field: DesignerField) {
  if (field.type === "Checkbox") {
    return "true";
  }

  if (field.type === "Select" || field.type === "Radio") {
    return field.options.map((option) => option.trim()).find(Boolean) ?? "";
  }

  return "";
}

function validateRequiredWhenRule(field: DesignerField, rule: ValidationRule, dependency: DesignerField | undefined, language: Language) {
  if (rule.ruleType !== "RequiredWhen") {
    return undefined;
  }

  if (!rule.dependsOnFieldKey.trim()) {
    return translate(language, "form.validation.dependencyRequired");
  }

  if (!dependency) {
    return translate(language, "form.validation.dependencyMustExist");
  }

  if (dependency.id === field.id || dependency.key.trim() === field.key.trim()) {
    return translate(language, "form.validation.selfDependency");
  }

  if (!rule.expectedValue.trim()) {
    return translate(language, "form.validation.expectedValueRequired");
  }

  if (dependency.type === "Select" || dependency.type === "Radio") {
    const options = dependency.options.map((option) => option.trim()).filter(Boolean);
    if (!options.includes(rule.expectedValue.trim())) {
      return translate(language, "form.validation.expectedSelectOption");
    }
  }

  if (dependency.type === "Checkbox" && !["true", "false"].includes(rule.expectedValue.trim())) {
    return translate(language, "form.validation.expectedCheckbox");
  }

  return undefined;
}

function ExpectedValueInput({
  dependency,
  expectedValue,
  language,
  onChange,
}: {
  dependency?: DesignerField;
  expectedValue: string;
  language: Language;
  onChange: (expectedValue: string) => void;
}) {
  const label = translate(language, "form.designer.expectedValue");
  const selectValue = translate(language, "form.designer.selectValue");

  if (dependency?.type === "Select" || dependency?.type === "Radio") {
    const options = dependency.options.map((option) => option.trim()).filter(Boolean);

    return (
      <label>
        {label}
        <select value={expectedValue} onChange={(event) => onChange(event.target.value)}>
          <option value="">{selectValue}</option>
          {options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </label>
    );
  }

  if (dependency?.type === "Checkbox") {
    return (
      <label>
        {label}
        <select value={expectedValue} onChange={(event) => onChange(event.target.value)}>
          <option value="">{selectValue}</option>
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      </label>
    );
  }

  return (
    <label>
      {label}
      <input
        value={expectedValue}
        onChange={(event) => onChange(event.target.value)}
        placeholder={translate(language, "form.designer.expectedValuePlaceholder")}
      />
    </label>
  );
}
