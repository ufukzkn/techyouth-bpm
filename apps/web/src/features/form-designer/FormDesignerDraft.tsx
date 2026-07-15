"use client";

import {
  AlignLeft,
  Calendar,
  ChevronDown,
  ChevronUp,
  CircleDot,
  FileUp,
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
import { Fragment, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  closestCenter,
  type CollisionDetection,
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
  KeyboardSensor,
  pointerWithin,
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
import { CSS, getEventCoordinates } from "@dnd-kit/utilities";
import { InlineValueLoader, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
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
const fieldPaletteDropId = "field-palette-drop-zone";
const paletteDragDistanceThreshold = 8;
const paletteEndInsertTolerance = 24;
const fieldTypeIcons: Record<FieldType, LucideIcon> = {
  Text: Type,
  TextArea: AlignLeft,
  Number: Hash,
  Email: Mail,
  Select: List,
  Radio: CircleDot,
  Checkbox: SquareCheck,
  Date: Calendar,
  FileUpload: FileUp,
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
  const [hasLoadedForms, setHasLoadedForms] = useState(false);
  const [isSwitchingForm, setIsSwitchingForm] = useState(false);
  const [isCreatingNewForm, setIsCreatingNewForm] = useState(false);
  const [label, setLabel] = useState("Masraf merkezi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);
  const [isAddingManualField, setIsAddingManualField] = useState(false);
  const manualFieldFeedbackTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const newFormFeedbackTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [saveState, setSaveState] = useState<"idle" | "saving" | "success" | "error">("idle");
  const [message, setMessage] = useState(() => t("form.designer.notSaved"));
  const [highlightedFieldId, setHighlightedFieldId] = useState("");
  const [moveFeedback, setMoveFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [displacedFeedback, setDisplacedFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [paletteInsertIndex, setPaletteInsertIndex] = useState<number | null>(null);
  const [paletteDragGhost, setPaletteDragGhost] = useState<{
    fieldType: FieldType;
    x: number;
    y: number;
  } | null>(null);
  const paletteGhostFrameRef = useRef<number | null>(null);
  const pendingPaletteGhostCoordinatesRef = useRef<{ x: number; y: number } | null>(null);
  const lastPalettePointerYRef = useRef<number | null>(null);
  const activePaletteGhostFieldType = paletteDragGhost?.fieldType ?? null;
  const fieldErrors = useMemo(() => validateDesignerFields(fields, language), [fields, language]);
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;
  const fieldErrorSummary = useMemo(
    () => buildDesignerErrorSummary(fields, fieldErrors, language),
    [fieldErrors, fields, language],
  );
  const selectedFormName = savedForms.find((form) => form.id === selectedFormId)?.name;
  const isInitialDesignerLoading = Boolean(token) && !hasLoadedForms;
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
  const paletteAwareCollisionDetection = useCallback<CollisionDetection>(
    (args) => {
      if (!isPaletteDragId(args.active.id)) {
        return closestCenter(args);
      }

      lastPalettePointerYRef.current = args.pointerCoordinates?.y ?? null;
      const collisions = pointerWithin(args);

      return collisions.sort((left, right) => {
        const leftIsField = fields.some((field) => field.id === left.id);
        const rightIsField = fields.some((field) => field.id === right.id);
        if (leftIsField !== rightIsField) {
          return leftIsField ? -1 : 1;
        }

        if (left.id === fieldCanvasDropId && right.id !== fieldCanvasDropId) {
          return 1;
        }
        if (right.id === fieldCanvasDropId && left.id !== fieldCanvasDropId) {
          return -1;
        }

        return 0;
      });
    },
    [fields],
  );

  useEffect(
    () => () => {
      if (manualFieldFeedbackTimeoutRef.current) {
        clearTimeout(manualFieldFeedbackTimeoutRef.current);
      }
      if (newFormFeedbackTimeoutRef.current) {
        clearTimeout(newFormFeedbackTimeoutRef.current);
      }
    },
    [],
  );

  useEffect(() => {
    if (!activePaletteGhostFieldType) {
      return;
    }

    function handlePalettePointerMove(event: PointerEvent) {
      pendingPaletteGhostCoordinatesRef.current = { x: event.clientX, y: event.clientY };
      if (paletteGhostFrameRef.current !== null) {
        return;
      }

      paletteGhostFrameRef.current = window.requestAnimationFrame(() => {
        paletteGhostFrameRef.current = null;
        const coordinates = pendingPaletteGhostCoordinatesRef.current;
        if (!coordinates) {
          return;
        }

        setPaletteDragGhost((current) =>
          current?.fieldType === activePaletteGhostFieldType
            ? { ...current, x: coordinates.x, y: coordinates.y }
            : current,
        );
      });
    }

    window.addEventListener("pointermove", handlePalettePointerMove, { passive: true });

    return () => {
      window.removeEventListener("pointermove", handlePalettePointerMove);
      if (paletteGhostFrameRef.current !== null) {
        window.cancelAnimationFrame(paletteGhostFrameRef.current);
        paletteGhostFrameRef.current = null;
      }
      pendingPaletteGhostCoordinatesRef.current = null;
    };
  }, [activePaletteGhostFieldType]);

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
          setMessage(formatDesignerApiError(error, language, t("form.designer.loadFailed")));
        }
      } finally {
        if (!ignore) {
          setIsLoadingForms(false);
          setHasLoadedForms(true);
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

    const timeoutId = window.setTimeout(() => setHighlightedFieldId(""), 900);
    return () => window.clearTimeout(timeoutId);
  }, [highlightedFieldId]);

  useEffect(() => {
    if (!moveFeedback) {
      return;
    }

    const timeoutId = window.setTimeout(() => setMoveFeedback(null), 680);
    return () => window.clearTimeout(timeoutId);
  }, [moveFeedback]);

  useEffect(() => {
    if (!displacedFeedback) {
      return;
    }

    const timeoutId = window.setTimeout(() => setDisplacedFeedback(null), 680);
    return () => window.clearTimeout(timeoutId);
  }, [displacedFeedback]);

  function handleDragStart(event: DragStartEvent) {
    if (!isPaletteDragId(event.active.id)) {
      setPaletteDragGhost(null);
      return;
    }

    const fieldType = event.active.data.current?.fieldType;
    const pointerCoordinates = getEventCoordinates(event.activatorEvent);
    setPaletteDragGhost(
      isSupportedFieldType(fieldType) && pointerCoordinates
        ? { fieldType, x: pointerCoordinates.x, y: pointerCoordinates.y }
        : null,
    );
    setPaletteInsertIndex(null);
    lastPalettePointerYRef.current = null;
  }

  function handleDragOver(event: DragOverEvent) {
    if (!isPaletteDragId(event.active.id)) {
      return;
    }

    setPaletteInsertIndex(resolvePaletteInsertIndex(event, fields, lastPalettePointerYRef.current));
  }

  function handleDragCancel(event: DragCancelEvent) {
    if (isPaletteDragId(event.active.id)) {
      setPaletteDragGhost(null);
      setPaletteInsertIndex(null);
      lastPalettePointerYRef.current = null;
    }
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;

    if (isPaletteDragId(active.id)) {
      const fieldType = active.data.current?.fieldType;
      const hasValidDropTarget = Boolean(
        over && (over.id === fieldCanvasDropId || fields.some((field) => field.id === over.id)),
      );
      if (
        paletteInsertIndex !== null &&
        hasValidDropTarget &&
        hasPaletteDragDistance(event.delta) &&
        isSupportedFieldType(fieldType)
      ) {
        addFieldFromPalette(fieldType, paletteInsertIndex);
      }
      setPaletteDragGhost(null);
      setPaletteInsertIndex(null);
      lastPalettePointerYRef.current = null;
      return;
    }

    if (!over) {
      setPaletteInsertIndex(null);
      return;
    }

    if (active.id === over.id) {
      return;
    }

    const activeFieldIndex = fields.findIndex((field) => field.id === active.id);
    const overFieldIndex = fields.findIndex((field) => field.id === over.id);
    if (activeFieldIndex < 0 || overFieldIndex < 0) {
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
    setIsAddingManualField(true);
    if (manualFieldFeedbackTimeoutRef.current) {
      clearTimeout(manualFieldFeedbackTimeoutRef.current);
    }
    manualFieldFeedbackTimeoutRef.current = setTimeout(() => {
      setIsAddingManualField(false);
      manualFieldFeedbackTimeoutRef.current = null;
    }, 240);
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
    const currentIndex = fields.findIndex((field) => field.id === id);
    const targetIndex = currentIndex + direction;
    if (currentIndex < 0 || targetIndex < 0 || targetIndex >= fields.length) {
      return;
    }
    const displacedFieldId = fields[targetIndex].id;

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
    setMoveFeedback({ id, direction });
    setDisplacedFeedback({ id: displacedFieldId, direction: direction === -1 ? 1 : -1 });
    triggerFieldHighlight(id);
    markUnsaved();
  }

  function triggerFieldHighlight(id: string) {
    setHighlightedFieldId("");
    window.requestAnimationFrame(() => setHighlightedFieldId(id));
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
    setIsSwitchingForm(true);
    const minimumTransition = new Promise<void>((resolve) => window.setTimeout(resolve, 240));

    if (!id) {
      await minimumTransition;
      resetDesigner();
      setIsSwitchingForm(false);
      return;
    }

    if (!token) {
      await minimumTransition;
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredLoad"));
      setIsSwitchingForm(false);
      return;
    }

    try {
      setIsLoadingForms(true);
      const [form] = await Promise.all([api.getForm(token, id), minimumTransition]);
      setSelectedFormId(form.id);
      setFormName(form.name);
      setDescription(form.description);
      setFields(toDesignerFields(form));
      setSaveState("idle");
      setMessage(t("form.designer.loadedForEdit", { name: form.name }));
    } catch (error) {
      await minimumTransition;
      setSaveState("error");
      setMessage(formatDesignerApiError(error, language, t("form.designer.formLoadFailed")));
    } finally {
      setIsLoadingForms(false);
      setIsSwitchingForm(false);
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

  function startNewForm() {
    resetDesigner();
    setIsCreatingNewForm(true);
    if (newFormFeedbackTimeoutRef.current) {
      clearTimeout(newFormFeedbackTimeoutRef.current);
    }
    newFormFeedbackTimeoutRef.current = setTimeout(() => {
      setIsCreatingNewForm(false);
      newFormFeedbackTimeoutRef.current = null;
    }, 240);
  }

  async function saveForm() {
    if (!token) {
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredSave"));
      return;
    }

    if (hasFieldErrors) {
      setSaveState("error");
      setMessage(fieldErrorSummary.text);
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
      setMessage(formatDesignerApiError(error, language, t("form.designer.saveFailed")));
    }
  }

  return (
    <section className={`designer-section${isInitialDesignerLoading ? " designer-section-initial-loading" : ""}`}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("form.designer.eyebrow")}</span>
          <h2>{t("form.designer.title")}</h2>
        </div>
        <p>{t("form.designer.description")}</p>
      </div>

      {isInitialDesignerLoading ? <FormDesignerOpeningSkeleton label={t("form.designer.loadingForms")} /> : null}

      <DndContext
        sensors={sensors}
        collisionDetection={paletteAwareCollisionDetection}
        onDragCancel={handleDragCancel}
        onDragEnd={handleDragEnd}
        onDragOver={handleDragOver}
        onDragStart={handleDragStart}
      >
        <div className="designer-grid">
          <div className="tool-panel designer-form-info-panel" aria-busy={isSwitchingForm || isCreatingNewForm}>
            {isSwitchingForm || isCreatingNewForm ? (
              <div className="designer-form-transition-overlay" role="status" aria-live="polite">
                <span className="designer-form-transition-indicator">
                  <InlineValueLoader
                    label={isCreatingNewForm ? t("form.designer.preparingNewForm") : t("form.designer.loadingForms")}
                  />
                </span>
              </div>
            ) : null}
            <h3>{t("form.designer.formInfo")}</h3>
            {isLoadingForms && !isSwitchingForm ? (
              <div className="designer-loading-state" aria-live="polite">
                <InlineValueLoader label={t("form.designer.loadingForms")} />
                <span>{t("form.designer.loadingForms")}</span>
              </div>
            ) : null}
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
            <button
              className="secondary-button"
              disabled={saveState === "saving"}
              type="button"
              onClick={startNewForm}
            >
              <Plus size={18} />
              {t("form.designer.newForm")}
            </button>
            <ol className="demo-steps" aria-label={t("form.designer.demoStepsAria")}>
              <li>{t("form.designer.demoStepEdit")}</li>
              <li>{t("form.designer.demoStepOptions")}</li>
              <li>{t("form.designer.demoStepRequiredWhen")}</li>
              <li>{t("form.designer.demoStepOrdering")}</li>
            </ol>
          </div>

          <div className="tool-panel designer-manual-field-panel" aria-busy={isAddingManualField}>
            {isAddingManualField ? (
              <div className="designer-manual-field-overlay" role="status" aria-live="polite">
                <span className="designer-manual-field-indicator">
                  <InlineValueLoader label={t("form.designer.addingField")} />
                  <span>{t("form.designer.addingField")}</span>
                </span>
              </div>
            ) : null}
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
                <Fragment key={field.id}>
                  {paletteInsertIndex === index ? (
                    <div className="field-insert-indicator field-insert-indicator-preview" aria-hidden="true" />
                  ) : null}
                  <SortableFieldCard id={field.id}>
                  {({ attributes, listeners, setActivatorNodeRef, isDragging }) => (
                    <>
                      <article
                        className={`field-card field-editor${isDragging ? " field-editor-dragging" : ""}${
                          highlightedFieldId === field.id ? " field-editor-highlighted" : ""
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

                      {field.type === "FileUpload" ? (
                        <div className="file-upload-policy-note">
                          <strong>{t("form.fileUpload.policyTitle")}</strong>
                          <span>{t("form.fileUpload.policyDescription")}</span>
                          <span>{t("form.fileUpload.metadataNote")}</span>
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
                </Fragment>
              ))}
              {paletteInsertIndex === fields.length ? (
                <div className="field-insert-indicator field-insert-indicator-preview" aria-hidden="true" />
              ) : null}
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

          <FieldPaletteRail label={t("form.designer.fieldPaletteStickyTitle")}>
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
            <div className="designer-save-panel">
              <button
                className="primary-button"
                disabled={saveState === "saving"}
                type="button"
                onClick={saveForm}
              >
                <Save size={18} />
                {saveState === "saving"
                  ? t("form.designer.saving")
                  : selectedFormId
                    ? t("form.designer.updateForm")
                    : t("form.designer.saveForm")}
              </button>
              {hasFieldErrors ? (
                <div className="field-error designer-blocking-error" role="alert">
                  <strong>{t("form.designer.saveBlockedTitle")}</strong>
                  {fieldErrorSummary.messages.length === 1 ? (
                    <span>{fieldErrorSummary.messages[0]}</span>
                  ) : (
                    <ul>
                      {fieldErrorSummary.messages.map((errorMessage) => (
                        <li key={errorMessage}>{errorMessage}</li>
                      ))}
                    </ul>
                  )}
                  {fieldErrorSummary.remainingCount > 0 ? (
                    <span className="designer-blocking-error-more">
                      {t("form.designer.moreErrors", { count: fieldErrorSummary.remainingCount })}
                    </span>
                  ) : null}
                </div>
              ) : null}
              <p className={`status-line status-line-${saveState}`} aria-live="polite">
                {message}
              </p>
            </div>
          </FieldPaletteRail>
        </div>
      </DndContext>
      {paletteDragGhost && typeof document !== "undefined" && document.body
        ? createPortal(
            <div
              className="field-palette-drag-ghost"
              style={{ left: paletteDragGhost.x, top: paletteDragGhost.y }}
              aria-hidden="true"
            >
              <PaletteFieldTypeDragGhost fieldType={paletteDragGhost.fieldType} language={language} />
            </div>,
            document.body,
          )
        : null}
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

function FormDesignerOpeningSkeleton({ label }: { label: string }) {
  return (
    <div className="form-opening-skeleton form-designer-opening-skeleton" role="status" aria-label={label}>
      <div className="form-opening-heading">
        <InlineValueLoader label={label} />
        <strong>{label}</strong>
      </div>
      <div className="form-opening-grid">
        <div className="form-opening-panel">
          <SkeletonBlock className="form-opening-title" />
          <SkeletonBlock className="form-opening-control" />
          <SkeletonBlock className="form-opening-control" />
          <SkeletonBlock className="form-opening-control" />
        </div>
        <div className="form-opening-panel form-opening-palette">
          <SkeletonBlock className="form-opening-title" />
          <SkeletonBlock className="form-opening-palette-row" />
          <SkeletonBlock className="form-opening-palette-row" />
          <SkeletonBlock className="form-opening-palette-row" />
        </div>
      </div>
      <div className="form-opening-fields">
        <div className="form-opening-field-guide">
          <SkeletonBlock className="form-opening-field-guide-title" />
          <SkeletonBlock className="form-opening-field-guide-copy" />
        </div>
        {Array.from({ length: 2 }, (_, index) => (
          <div className="form-opening-field-card" key={index}>
            <div className="form-opening-field-header">
              <div className="form-opening-field-heading">
                <SkeletonBlock className="form-opening-field-title" />
                <SkeletonBlock className="form-opening-field-meta" />
              </div>
              <div className="form-opening-field-actions">
                <SkeletonBlock className="form-opening-field-drag" />
                <SkeletonBlock className="form-opening-field-action" />
                <SkeletonBlock className="form-opening-field-action" />
              </div>
            </div>
            <div className="form-opening-field-controls">
              {Array.from({ length: 3 }, (_, controlIndex) => (
                <div className="form-opening-field-control" key={controlIndex}>
                  <SkeletonBlock className="form-opening-field-label" />
                  <SkeletonBlock className="form-opening-field-input" />
                </div>
              ))}
              <SkeletonBlock className="form-opening-field-required" />
            </div>
          </div>
        ))}
      </div>
    </div>
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
  const style = isDragging ? undefined : { transform: CSS.Translate.toString(transform) };

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

function PaletteFieldTypeDragGhost({ fieldType, language }: { fieldType: FieldType; language: Language }) {
  const label = fieldTypeLabel(language, fieldType);
  const description = translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey);
  const FieldTypeIcon = fieldTypeIcons[fieldType];

  return (
    <div className="field-palette-item field-palette-drag-ghost-card">
      <span className="field-palette-icon">
        <FieldTypeIcon size={18} />
      </span>
      <span className="field-palette-item-copy">
        <strong>{label}</strong>
        <span>{description}</span>
      </span>
      <GripVertical className="field-palette-grip" size={16} />
    </div>
  );
}

function FieldCanvasDropZone({ children, label }: { children: ReactNode; label: string }) {
  const { isOver, setNodeRef } = useDroppable({ id: fieldCanvasDropId });

  return (
    <div id={fieldCanvasDropId} ref={setNodeRef} className={`field-list${isOver ? " field-list-drop-active" : ""}`} aria-label={label}>
      {children}
    </div>
  );
}

function FieldPaletteRail({ children, label }: { children: ReactNode; label: string }) {
  const { isOver, setNodeRef } = useDroppable({ id: fieldPaletteDropId });

  return (
    <aside
      ref={setNodeRef}
      className={`field-palette-rail${isOver ? " field-palette-rail-drop-active" : ""}`}
      aria-label={label}
    >
      {children}
    </aside>
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
  const { attributes, listeners, setActivatorNodeRef, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    transition: {
      duration: 300,
      easing: "cubic-bezier(0.2, 0.8, 0.2, 1)",
    },
  });
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div
      ref={setNodeRef}
      data-designer-field-id={id}
      className={`sortable-field-card${isDragging ? " sortable-field-card-dragging" : ""}`}
      style={style}
    >
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

function resolvePaletteInsertIndex(
  event: DragOverEvent | DragEndEvent,
  fields: DesignerField[],
  palettePointerY: number | null,
) {
  const over = event.over;
  if (!over) {
    return null;
  }

  if (over.id === fieldCanvasDropId && fields.length === 0) {
    return 0;
  }

  const directlyOverField = fields.some((field) => field.id === over.id);
  const fieldCollision = directlyOverField
    ? null
    : event.collisions?.find((collision) => fields.some((field) => field.id === collision.id));
  const targetFieldId = directlyOverField ? over.id : fieldCollision?.id;
  const overFieldIndex = fields.findIndex((field) => field.id === targetFieldId);
  if (overFieldIndex < 0) {
    if (over.id !== fieldCanvasDropId || palettePointerY === null) {
      return null;
    }

    const lastField = fields[fields.length - 1];
    const lastFieldRect = lastField ? getDesignerFieldRect(lastField.id) : null;
    if (!lastFieldRect) {
      return null;
    }

    const isWithinEndTolerance =
      palettePointerY >= lastFieldRect.bottom &&
      palettePointerY <= lastFieldRect.bottom + paletteEndInsertTolerance;
    return isWithinEndTolerance ? fields.length : null;
  }

  const targetRect = directlyOverField ? over.rect : fieldCollision?.data?.droppableContainer?.rect.current;
  if (!targetRect) {
    return null;
  }

  const activeRect = event.active.rect.current.translated;
  const activeAnchorY =
    palettePointerY ?? (activeRect ? activeRect.top + Math.min(activeRect.height / 2, 28) : null);
  if (activeAnchorY === null) {
    return null;
  }

  const targetMiddleY = targetRect.top + targetRect.height / 2;
  return activeAnchorY > targetMiddleY ? overFieldIndex + 1 : overFieldIndex;
}

function getDesignerFieldRect(fieldId: string) {
  if (typeof document === "undefined") {
    return null;
  }

  const fieldElement = Array.from(document.querySelectorAll<HTMLElement>("[data-designer-field-id]")).find(
    (element) => element.dataset.designerFieldId === fieldId,
  );
  return fieldElement?.getBoundingClientRect() ?? null;
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

function formatDesignerApiError(error: unknown, language: Language, fallback: string) {
  if (!(error instanceof ApiError)) {
    return fallback;
  }

  return error.errors
    .map((message) =>
      message === "A community is required for form definitions."
        ? translate(language, "form.designer.apiCommunityRequired")
        : message,
    )
    .join(" ");
}

function buildDesignerErrorSummary(fields: DesignerField[], errors: DesignerFieldErrors, language: Language) {
  const messages: string[] = [];

  for (const field of fields) {
    const error = errors[field.id];
    if (!error) {
      continue;
    }

    const fieldName = field.label.trim() || field.key.trim() || translate(language, "form.designer.untitledField");
    if (error.key) {
      messages.push(
        field.key.trim()
          ? translate(language, "form.designer.errorDuplicateKey", { key: field.key.trim() })
          : translate(language, "form.designer.errorEmptyKey"),
      );
    }
    if (error.label) {
      messages.push(translate(language, "form.designer.errorEmptyLabel"));
    }
    if (error.options) {
      const filledOptions = field.options.map((option) => option.trim()).filter(Boolean);
      const duplicateOption = filledOptions.find(
        (option, index) =>
          filledOptions.findIndex((candidate) => candidate.toLocaleLowerCase("tr") === option.toLocaleLowerCase("tr")) !== index,
      );
      if (filledOptions.length === 0) {
        messages.push(translate(language, "form.designer.errorOptionsRequired", { field: fieldName }));
      } else if (field.options.some((option) => !option.trim())) {
        messages.push(translate(language, "form.designer.errorEmptyOption", { field: fieldName }));
      } else if (duplicateOption) {
        messages.push(
          translate(language, "form.designer.errorDuplicateOption", { field: fieldName, option: duplicateOption }),
        );
      }
    }
    for (const ruleError of Object.values(error.rules ?? {})) {
      messages.push(translate(language, "form.designer.errorDependentRule", { field: fieldName, error: ruleError }));
    }
  }

  const visibleMessages = messages.slice(0, 3);
  const remainingCount = messages.length - visibleMessages.length;
  const textParts = [...visibleMessages];
  if (remainingCount > 0) {
    textParts.push(translate(language, "form.designer.moreErrors", { count: remainingCount }));
  }

  return {
    messages: visibleMessages,
    remainingCount,
    text: textParts.join(" ") || translate(language, "form.designer.blockingErrors"),
  };
}

function validateDesignerFields(fields: DesignerField[], language: Language) {
  const errors: DesignerFieldErrors = {};
  const keyCounts = fields.reduce<Record<string, number>>((current, field) => {
    if (!field.key.trim()) {
      return current;
    }

    const key = createDesignerFieldKey(field.key, field.sortOrder).toLowerCase();
    current[key] = (current[key] ?? 0) + 1;

    return current;
  }, {});

  for (const field of fields) {
    const fieldError: DesignerFieldErrors[string] = {};
    const key = createDesignerFieldKey(field.key, field.sortOrder);

    if (!field.key.trim()) {
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
