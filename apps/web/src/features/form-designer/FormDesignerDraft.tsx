"use client";

import {
  FilePlus2,
  Plus,
} from "lucide-react";
import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  arrayMove,
  horizontalListSortingStrategy,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { getEventCoordinates } from "@dnd-kit/utilities";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { FormFieldEditor } from "@/features/form-designer/FormFieldEditor";
import {
  FormPrimaryVersionActions,
  FormVersionActionRail,
  type FormSaveState,
} from "@/features/form-designer/FormVersionActions";
import { MobileFieldPalette } from "@/features/form-designer/MobileFieldPalette";
import {
  FieldCanvasDropZone,
  FieldPaletteRail,
  FormDesignerOpeningSkeleton,
  PaletteFieldTypeCard,
  PaletteFieldTypeDragGhost,
  SortablePageTab,
  fieldTypeIcons,
} from "@/features/form-designer/FormDesignerComponents";
import { JsonViewer } from "@/features/ui/JsonViewer";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import {
  createDefaultField,
  createDefaultOptions,
  fieldTypeLabel,
  fieldTypeUsesOptions,
  supportedFieldTypes,
} from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import {
  type FormVersionAdapter,
  type VersionedFormLayout,
} from "@/features/forms/formVersioning";
import {
  createDesignerFieldKey,
  createInitialDesignerPages,
  createInitialPageId,
  createVersionedLayout,
  findDesignerFieldLocation,
  findFirstDependencyField,
  fieldCanvasDropId,
  flattenDesignerFields,
  getDefaultExpectedValue,
  getPageDragId,
  getPageIdFromDragId,
  getPaletteFieldDefaultLabel,
  hasPaletteDragDistance,
  isPageDragId,
  isPaletteDragId,
  isSupportedFieldType,
  moveFieldBetweenPages,
  moveFieldWithinPage,
  normalizeDesignerPages,
  paletteDragDistanceThreshold,
  removeDesignerPage,
  reorderDesignerPages,
  reorderFieldsInPage,
  resolvePaletteInsertIndex,
  toDesignerPages,
  updateDesignerField,
  upsertForm,
  validateDesignerFields,
  type DesignerField,
  type DesignerFieldErrors,
  type DesignerPage,
  type DesignerVersionState,
} from "@/features/form-designer/formDesignerModel";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { Community, CreateFormRequest, FieldType, FormDefinition, Language, ValidationRule } from "@/lib/types";

type DesignerSaveFieldErrorSource = "client" | "api";
type DesignerSaveFieldError = DesignerFieldErrors[string] & { source: DesignerSaveFieldErrorSource };
type DesignerSaveFieldErrors = Record<string, DesignerSaveFieldError>;

export type FormDesignerDraftProps = {
  versionAdapter?: FormVersionAdapter;
};

export function FormDesignerDraft({ versionAdapter }: FormDesignerDraftProps = {}) {
  const token = useSessionStore((state) => state.token);
  const user = useSessionStore((state) => state.user);
  const language = useSessionStore((state) => state.language);
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const pagingCopy = getFormPagingCopy(language);
  const [pages, setPages] = useState<DesignerPage[]>(() => createInitialDesignerPages(language));
  const [activePageId, setActivePageId] = useState(() => createInitialPageId());
  const [versionState, setVersionState] = useState<DesignerVersionState>({ version: 1, status: "draft" });
  const [formName, setFormName] = useState("Demo Süreç Formu");
  const [description, setDescription] = useState("Frontend tarafında tasarlanan form modeli");
  const [savedForms, setSavedForms] = useState<FormDefinition[]>([]);
  const [selectedFormId, setSelectedFormId] = useState("");
  const [communities, setCommunities] = useState<Community[]>([]);
  const [selectedCommunityId, setSelectedCommunityId] = useState(() => user?.communityId ?? "");
  const [isLoadingForms, setIsLoadingForms] = useState(false);
  const [isLoadingCommunities, setIsLoadingCommunities] = useState(false);
  const [showCommunityError, setShowCommunityError] = useState(false);
  const [hasLoadedForms, setHasLoadedForms] = useState(false);
  const [isSwitchingForm, setIsSwitchingForm] = useState(false);
  const [isCreatingNewForm, setIsCreatingNewForm] = useState(false);
  const [label, setLabel] = useState("Masraf merkezi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);
  const [isAddingManualField, setIsAddingManualField] = useState(false);
  const manualFieldFeedbackTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const newFormFeedbackTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [saveState, setSaveState] = useState<FormSaveState>("idle");
  const [saveFieldErrors, setSaveFieldErrors] = useState<DesignerSaveFieldErrors>({});
  const [isArchiveConfirmationOpen, setIsArchiveConfirmationOpen] = useState(false);
  const [message, setMessage] = useState(() => t("form.designer.notSaved"));
  const [highlightedFieldId, setHighlightedFieldId] = useState("");
  const [moveFeedback, setMoveFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [displacedFeedback, setDisplacedFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [recentlyMovedPage, setRecentlyMovedPage] = useState<{ id: string; direction: -1 | 1 } | null>(null);
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
  const fields = useMemo(() => flattenDesignerFields(pages), [pages]);
  const activePage = useMemo(
    () => pages.find((page) => page.id === activePageId) ?? pages[0],
    [activePageId, pages],
  );
  const activeFields = useMemo(() => activePage?.fields ?? [], [activePage]);
  const fieldErrors = useMemo(() => validateDesignerFields(fields, language), [fields, language]);
  const hasPageErrors = pages.some((page) => page.title.trim().length === 0);
  const hasFieldErrors = Object.keys(fieldErrors).length > 0 || hasPageErrors;
  const fieldErrorSummary = useMemo(
    () => buildDesignerErrorSummary(fields, fieldErrors, language, hasPageErrors ? pagingCopy.pageTitleRequired : undefined),
    [fieldErrors, fields, hasPageErrors, language, pagingCopy.pageTitleRequired],
  );
  const isSuperAdmin = user?.role === "SuperAdmin";
  const hasCommunityError = Boolean(isSuperAdmin && !selectedCommunityId);
  const selectedFormName = savedForms.find((form) => form.id === selectedFormId)?.name;
  const isInitialDesignerLoading = Boolean(token) && !hasLoadedForms;
  const isPersisting = saveState === "saving" || saveState === "publishing";
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
        const isExistingFieldDrag = fields.some((field) => field.id === args.active.id);
        if (!isExistingFieldDrag) return closestCenter(args);

        const validIds = new Set([
          ...activeFields.map((field) => field.id),
          ...pages.map((page) => getPageDragId(page.id)),
        ]);
        const pointerTargets = pointerWithin(args).filter((collision) => validIds.has(String(collision.id)));
        return pointerTargets.length > 0
          ? pointerTargets
          : closestCenter(args).filter((collision) => validIds.has(String(collision.id)));
      }

      lastPalettePointerYRef.current = args.pointerCoordinates?.y ?? null;
      return pointerWithin(args).sort((left, right) => {
        const leftIsField = activeFields.some((field) => field.id === left.id);
        const rightIsField = activeFields.some((field) => field.id === right.id);
        if (leftIsField !== rightIsField) return leftIsField ? -1 : 1;
        if (left.id === fieldCanvasDropId && right.id !== fieldCanvasDropId) return 1;
        if (right.id === fieldCanvasDropId && left.id !== fieldCanvasDropId) return -1;
        return 0;
      });
    },
    [activeFields, fields, pages],
  );

  const formModel = useMemo<CreateFormRequest>(
    () => ({
      name: formName,
      description,
      communityId: selectedCommunityId || user?.communityId || undefined,
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
    [description, fields, formName, selectedCommunityId, user?.communityId],
  );
  const layoutModel = useMemo(
    () => createVersionedLayout(pages, formModel, versionState),
    [formModel, pages, versionState],
  );
  const previewModel = useMemo(
    () => ({
      ...formModel,
      version: layoutModel.version,
      status: layoutModel.status,
      pages: layoutModel.pages,
    }),
    [formModel, layoutModel],
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
    const activeFieldType = activePaletteGhostFieldType;
    if (!activeFieldType) return;

    function handlePalettePointerMove(event: PointerEvent) {
      pendingPaletteGhostCoordinatesRef.current = { x: event.clientX, y: event.clientY };
      if (paletteGhostFrameRef.current !== null) return;

      paletteGhostFrameRef.current = window.requestAnimationFrame(() => {
        paletteGhostFrameRef.current = null;
        const coordinates = pendingPaletteGhostCoordinatesRef.current;
        if (!coordinates) return;
        setPaletteDragGhost((current) =>
          current?.fieldType === activeFieldType ? { ...current, ...coordinates } : current,
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
          setMessage(localizeApiError(error, language, t("form.designer.loadFailed")));
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
    if (!token || !isSuperAdmin) {
      return;
    }

    let ignore = false;
    const activeToken = token;

    async function loadCommunities() {
      try {
        setIsLoadingCommunities(true);
        const result = await api.listCommunities(activeToken);
        if (!ignore) {
          setCommunities(result);
        }
      } catch (error) {
        if (!ignore) {
          setMessage(localizeApiError(error, language, t("form.designer.communityLoadFailed")));
        }
      } finally {
        if (!ignore) {
          setIsLoadingCommunities(false);
        }
      }
    }

    loadCommunities();

    return () => {
      ignore = true;
    };
  }, [isSuperAdmin, language, token, t]);

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

  useEffect(() => {
    if (!recentlyMovedPage) {
      return;
    }

    const timeoutId = window.setTimeout(() => setRecentlyMovedPage(null), 440);
    return () => window.clearTimeout(timeoutId);
  }, [recentlyMovedPage]);

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

    setPaletteInsertIndex(resolvePaletteInsertIndex(event, activeFields, lastPalettePointerYRef.current));
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
        over && (over.id === fieldCanvasDropId || activeFields.some((field) => field.id === over.id)),
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

    if (isPageDragId(active.id)) {
      if (over && isPageDragId(over.id) && active.id !== over.id) {
        const activeId = getPageIdFromDragId(active.id);
        const overId = getPageIdFromDragId(over.id);
        setPages((current) => reorderDesignerPages(current, activeId, overId));
        markUnsaved();
      }
      return;
    }

    if (!over) {
      setPaletteInsertIndex(null);
      return;
    }

    const destinationPageId = getPageIdFromDragId(over.id);
    if (destinationPageId) {
      moveFieldToPage(String(active.id), destinationPageId);
      return;
    }

    if (active.id === over.id) {
      return;
    }

    const activeFieldIndex = activeFields.findIndex((field) => field.id === active.id);
    const overFieldIndex = activeFields.findIndex((field) => field.id === over.id);
    if (activeFieldIndex < 0 || overFieldIndex < 0) {
      return;
    }

    setPages((current) => reorderFieldsInPage(current, activePageId, String(active.id), String(over.id)));
    markUnsaved();
  }

  function addFieldFromPalette(fieldType: FieldType, insertIndex: number) {
    const defaultLabel = getPaletteFieldDefaultLabel(language, fieldType);
    const addedFieldId = `palette-${fieldType}-${Date.now()}`;

    setPages((current) => {
      const page = current.find((candidate) => candidate.id === activePageId) ?? current[0];
      if (!page) {
        return current;
      }

      const safeInsertIndex = Math.min(Math.max(insertIndex, 0), page.fields.length);
      const field = createDefaultField({
        label: defaultLabel,
        type: fieldType,
        required: false,
        sortOrder: fields.length + 1,
        language,
      });
      const nextField: DesignerField = {
        ...field,
        id: addedFieldId,
      };
      const nextFields = [...page.fields];
      nextFields.splice(safeInsertIndex, 0, nextField);

      return normalizeDesignerPages(
        current.map((candidate) => (candidate.id === page.id ? { ...candidate, fields: nextFields } : candidate)),
      );
    });
    setSaveState("idle");
    setMessage(t("form.designer.fieldAddedFromPalette", { label: defaultLabel }));
    markVersionAsDraft();
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

    setPages((current) =>
      normalizeDesignerPages(
        current.map((page, index) =>
          page.id === activePageId || (!current.some((candidate) => candidate.id === activePageId) && index === 0)
            ? { ...page, fields: [...page.fields, nextField] }
            : page,
        ),
      ),
    );
    setLabel("");
    setType("Text");
    setRequired(false);
    setSaveState("idle");
    setMessage(t("form.designer.unsaved"));
    markVersionAsDraft();
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
    setPages((current) => updateDesignerField(current, id, (field) => ({ ...field, ...patch })));
    clearSaveFieldError(id);
    markUnsaved();
  }

  function updateFieldType(id: string, nextType: FieldType) {
    setPages((current) =>
      updateDesignerField(current, id, (field) => {
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
    clearSaveFieldError(id);
    markUnsaved();
  }

  function removeField(id: string) {
    setPages((current) =>
      normalizeDesignerPages(
        current.map((page) => ({ ...page, fields: page.fields.filter((field) => field.id !== id) })),
      ),
    );
    clearSaveFieldError(id);
    markUnsaved();
  }

  function toggleRequired(id: string) {
    setPages((current) =>
      updateDesignerField(current, id, (field) => ({ ...field, required: !field.required })),
    );
    clearSaveFieldError(id);
    markUnsaved();
  }

  function moveField(id: string, direction: -1 | 1) {
    const currentIndex = activeFields.findIndex((field) => field.id === id);
    const targetIndex = currentIndex + direction;
    if (currentIndex < 0 || targetIndex < 0 || targetIndex >= activeFields.length) {
      return;
    }
    const displacedFieldId = activeFields[targetIndex].id;

    setPages((current) => moveFieldWithinPage(current, activePageId, id, direction));
    setMoveFeedback({ id, direction });
    setDisplacedFeedback({ id: displacedFieldId, direction: direction === -1 ? 1 : -1 });
    triggerFieldHighlight(id);
    markUnsaved();
  }

  function triggerFieldHighlight(id: string) {
    setHighlightedFieldId("");
    window.requestAnimationFrame(() => setHighlightedFieldId(id));
  }

  function clearSaveFieldError(fieldId: string) {
    setSaveFieldErrors((current) => {
      if (!current[fieldId]) {
        return current;
      }

      const next = { ...current };
      delete next[fieldId];
      return next;
    });
  }

  function revealSaveFieldErrors(
    nextErrors: DesignerFieldErrors,
    source: DesignerSaveFieldErrorSource = "client",
  ) {
    const sourcedErrors = Object.fromEntries(
      Object.entries(nextErrors).map(([fieldId, error]) => [fieldId, { ...error, source }]),
    ) as DesignerSaveFieldErrors;
    setSaveFieldErrors(sourcedErrors);
    const firstInvalidField = fields.find((field) => sourcedErrors[field.id]);
    if (!firstInvalidField) {
      return;
    }

    const location = findDesignerFieldLocation(pages, firstInvalidField.id);
    if (location && location.page.id !== activePageId) {
      setActivePageId(location.page.id);
    }

    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        const behavior = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";
        document.getElementById(`designer-field-${firstInvalidField.id}`)?.scrollIntoView({
          behavior,
          block: "center",
        });
      });
    });
  }

  function handlePersistError(error: unknown) {
    const localizedMessage = localizeApiError(error, language, t("form.designer.saveFailed"));
    revealSaveFieldErrors(mapApiErrorToDesignerFields(error, fields, localizedMessage), "api");
    setSaveState("error");
    setMessage(localizedMessage);
  }

  function addPage() {
    const id = `page-${Date.now()}`;
    setPages((current) => [
      ...current,
      {
        id,
        title: `${pagingCopy.page} ${current.length + 1}`,
        description: "",
        fields: [],
      },
    ]);
    setActivePageId(id);
    setPaletteInsertIndex(null);
    markUnsaved();
  }

  function updatePage(id: string, patch: Partial<Pick<DesignerPage, "title" | "description">>) {
    setPages((current) => current.map((page) => (page.id === id ? { ...page, ...patch } : page)));
    markUnsaved();
  }

  function removePage(id: string) {
    if (pages.length <= 1) {
      return;
    }

    const removedIndex = pages.findIndex((page) => page.id === id);
    if (removedIndex < 0) {
      return;
    }

    const destination = pages[removedIndex - 1] ?? pages[removedIndex + 1];
    setPages((current) => removeDesignerPage(current, id, destination.id));
    if (activePageId === id) {
      setActivePageId(destination.id);
    }
    setPaletteInsertIndex(null);
    markUnsaved();
  }

  function movePage(id: string, direction: -1 | 1) {
    const currentIndex = pages.findIndex((page) => page.id === id);
    const targetIndex = currentIndex + direction;
    if (currentIndex < 0 || targetIndex < 0 || targetIndex >= pages.length) {
      return;
    }

    setPages((current) => normalizeDesignerPages(arrayMove(current, currentIndex, targetIndex)));
    setRecentlyMovedPage({ id, direction });
    markUnsaved();
  }

  function moveFieldToPage(fieldId: string, destinationPageId: string) {
    const source = findDesignerFieldLocation(pages, fieldId);
    if (!source || source.page.id === destinationPageId) {
      return;
    }

    setPages((current) => moveFieldBetweenPages(current, fieldId, destinationPageId));
    setActivePageId(destinationPageId);
    setPaletteInsertIndex(null);
    triggerFieldHighlight(fieldId);
    markUnsaved();
    window.requestAnimationFrame(() => {
      document.getElementById(`designer-field-${fieldId}`)?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    });
  }

  function addOption(fieldId: string) {
    setPages((current) =>
      updateDesignerField(current, fieldId, (field) => ({
        ...field,
        options: [...field.options, `Secenek ${field.options.length + 1}`],
      })),
    );
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function updateOption(fieldId: string, optionIndex: number, value: string) {
    setPages((current) =>
      updateDesignerField(current, fieldId, (field) => ({
        ...field,
        options: field.options.map((option, index) => (index === optionIndex ? value : option)),
      })),
    );
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function removeOption(fieldId: string, optionIndex: number) {
    setPages((current) =>
      updateDesignerField(current, fieldId, (field) => ({
        ...field,
        options: field.options.filter((_, index) => index !== optionIndex),
      })),
    );
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function addRequiredWhenRule(fieldId: string) {
    setPages((current) => {
      const currentFields = flattenDesignerFields(current);
      return updateDesignerField(current, fieldId, (field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = findFirstDependencyField(currentFields, field);
        const rule: ValidationRule = {
          ruleType: "RequiredWhen",
          dependsOnFieldKey: dependency?.key.trim() ?? "",
          expectedValue: dependency ? getDefaultExpectedValue(dependency) : "",
          message: t("form.validation.requiredWhenDefault", { label: field.label || field.key }),
        };

        return { ...field, validationRules: [...field.validationRules, rule] };
      });
    });
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function updateRequiredWhenRule(fieldId: string, ruleIndex: number, patch: Partial<ValidationRule>) {
    setPages((current) =>
      updateDesignerField(current, fieldId, (field) => ({
        ...field,
        validationRules: field.validationRules.map((rule, index) =>
          index === ruleIndex ? { ...rule, ...patch } : rule,
        ),
      })),
    );
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function updateRuleDependency(fieldId: string, ruleIndex: number, dependsOnFieldKey: string) {
    setPages((current) => {
      const currentFields = flattenDesignerFields(current);
      return updateDesignerField(current, fieldId, (field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = currentFields.find((candidate) => candidate.key.trim() === dependsOnFieldKey);
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
      });
    });
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function removeRequiredWhenRule(fieldId: string, ruleIndex: number) {
    setPages((current) =>
      updateDesignerField(current, fieldId, (field) => ({
        ...field,
        validationRules: field.validationRules.filter((_, index) => index !== ruleIndex),
      })),
    );
    clearSaveFieldError(fieldId);
    markUnsaved();
  }

  function markUnsaved() {
    setSaveState("idle");
    setMessage(t("form.designer.unsaved"));
    markVersionAsDraft();
  }

  function markVersionAsDraft() {
    setVersionState((current) =>
      current.status !== "draft"
        ? { versionId: undefined, version: current.version + 1, status: "draft" }
        : current,
    );
  }

  async function loadSavedForm(id: string) {
    setSelectedFormId(id);
    setSaveFieldErrors({});
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
      setSelectedCommunityId(form.communityId);
      setShowCommunityError(false);
      setFormName(form.name);
      setDescription(form.description);
      const adapterVersion = versionAdapter?.resolveVersion?.(form);
      const versionedForm = adapterVersion ? { ...form, fields: adapterVersion.fields } : form;
      const resolved = toDesignerPages(
        versionedForm,
        adapterVersion?.layout ?? versionAdapter?.resolveLayout?.(form),
        pagingCopy.page,
      );
      setPages(resolved.pages);
      setActivePageId(resolved.pages[0].id);
      setVersionState({
        versionId: resolved.layout.versionId,
        version: resolved.layout.version,
        status: resolved.layout.status,
      });
      setSaveState("idle");
      setMessage(t("form.designer.loadedForEdit", { name: form.name }));
    } catch (error) {
      await minimumTransition;
      setSaveState("error");
      setMessage(localizeApiError(error, language, t("form.designer.formLoadFailed")));
    } finally {
      setIsLoadingForms(false);
      setIsSwitchingForm(false);
    }
  }

  function resetDesigner() {
    const nextPages = createInitialDesignerPages(language);
    setSelectedFormId("");
    setSelectedCommunityId(user?.communityId ?? "");
    setShowCommunityError(false);
    setFormName("Demo Süreç Formu");
    setDescription("Frontend tarafında tasarlanan form modeli");
    setPages(nextPages);
    setActivePageId(nextPages[0].id);
    setVersionState({ version: 1, status: "draft" });
    setSaveFieldErrors({});
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

  async function saveDraft() {
    if (!token) {
      setSaveFieldErrors({});
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredSave"));
      return;
    }

    if (hasCommunityError) {
      setSaveFieldErrors({});
      setShowCommunityError(true);
      setSaveState("error");
      setMessage(t("form.designer.communityRequired"));
      return;
    }

    if (hasFieldErrors) {
      revealSaveFieldErrors(fieldErrors);
      setSaveState("error");
      setMessage(fieldErrorSummary.text);
      return;
    }

    try {
      setSaveFieldErrors({});
      setSaveState("saving");
      const isUpdate = selectedFormId.length > 0;
      const draftLayout: VersionedFormLayout =
        layoutModel.status !== "draft"
          ? { ...layoutModel, versionId: undefined, version: layoutModel.version + 1, status: "draft" }
          : { ...layoutModel, status: "draft" };
      const saved = await persistFlatForm();
      const adapterLayout = await versionAdapter?.saveDraft?.({ form: saved, request: formModel, layout: draftLayout });
      const nextLayout = adapterLayout ?? draftLayout;
      setSelectedFormId(saved.id);
      setSavedForms((current) => upsertForm(current, saved));
      applyPersistedForm(saved, nextLayout);
      setSaveState("success");
      setMessage(
        t("form.designer.savedMessage", {
          action: isUpdate ? t("form.designer.savedActionUpdated") : t("form.designer.savedActionCreated"),
          name: saved.name,
        }),
      );
    } catch (error) {
      handlePersistError(error);
    }
  }

  async function publishForm() {
    if (!versionAdapter?.publish) {
      return;
    }

    if (!token) {
      setSaveFieldErrors({});
      setSaveState("error");
      setMessage(t("form.designer.sessionRequiredSave"));
      return;
    }

    if (hasCommunityError) {
      setSaveFieldErrors({});
      setShowCommunityError(true);
      setSaveState("error");
      setMessage(t("form.designer.communityRequired"));
      return;
    }

    if (hasFieldErrors) {
      revealSaveFieldErrors(fieldErrors);
      setSaveState("error");
      setMessage(fieldErrorSummary.text);
      return;
    }

    try {
      setSaveFieldErrors({});
      setSaveState("publishing");
      const publishLayout: VersionedFormLayout = { ...layoutModel, status: "published" };
      const saved = await persistFlatForm();
      const adapterLayout = await versionAdapter.publish({ form: saved, request: formModel, layout: publishLayout });
      const nextLayout = adapterLayout ?? publishLayout;
      setSelectedFormId(saved.id);
      setSavedForms((current) => upsertForm(current, saved));
      applyPersistedForm(saved, nextLayout);
      setSaveState("success");
      setMessage(pagingCopy.publishedMessage);
    } catch (error) {
      handlePersistError(error);
    }
  }

  async function archiveForm() {
    const selectedForm = savedForms.find((form) => form.id === selectedFormId);
    if (!versionAdapter?.archive || !selectedForm || versionState.status !== "published") {
      return;
    }

    try {
      setIsArchiveConfirmationOpen(false);
      setSaveState("archiving");
      const archivedLayout = await versionAdapter.archive({ form: selectedForm, request: formModel, layout: layoutModel });
      if (archivedLayout) {
        applyPersistedForm(selectedForm, archivedLayout);
      }
      setSaveState("success");
      setMessage(pagingCopy.archivedMessage);
    } catch (error) {
      setSaveState("error");
      setMessage(localizeApiError(error, language, t("form.designer.saveFailed")));
    }
  }

  async function persistFlatForm() {
    if (!token) {
      throw new Error("A session is required to persist a form.");
    }

    return selectedFormId
      ? api.updateForm(token, selectedFormId, { ...formModel, createPublishedVersion: false })
      : api.createForm(token, { ...formModel, createPublishedVersion: false });
  }

  function applyPersistedForm(form: FormDefinition, layout: VersionedFormLayout) {
    const resolved = toDesignerPages(form, layout, pagingCopy.page);
    const nextActivePage = resolved.pages.find((page) => page.id === activePageId) ?? resolved.pages[0];
    setSaveFieldErrors({});
    setPages(resolved.pages);
    setActivePageId(nextActivePage.id);
    setVersionState({
      versionId: resolved.layout.versionId,
      version: resolved.layout.version,
      status: resolved.layout.status,
    });
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
                    {form.name}{isSuperAdmin ? ` · ${form.communityName}` : ""}
                  </option>
                ))}
              </select>
            </label>
            {isSuperAdmin ? (
              <label>
                {t("form.designer.communityLabel")}
                <select
                  disabled={isLoadingCommunities || Boolean(selectedFormId)}
                  value={selectedCommunityId}
                  onChange={(event) => {
                    setSelectedCommunityId(event.target.value);
                    setShowCommunityError(false);
                    markUnsaved();
                  }}
                >
                  <option value="">
                    {isLoadingCommunities
                      ? t("form.designer.loadingCommunities")
                      : t("form.designer.selectCommunity")}
                  </option>
                  {communities
                    .filter((community) => community.isActive || community.id === selectedCommunityId)
                    .map((community) => (
                      <option key={community.id} value={community.id}>
                        {community.name}{community.isActive ? "" : ` (${t("form.designer.inactiveCommunity")})`}
                      </option>
                    ))}
                </select>
                <span className="helper-copy">
                  {selectedFormId
                    ? t("form.designer.communityLocked")
                    : t("form.designer.communityHelp")}
                </span>
                {showCommunityError ? <span className="field-error">{t("form.designer.communityRequired")}</span> : null}
              </label>
            ) : null}
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
            <FormPrimaryVersionActions
              canArchive={Boolean(versionAdapter?.archive)}
              canPublish={Boolean(versionAdapter?.publish)}
              canSaveDraft={Boolean(versionAdapter?.saveDraft)}
              isPersisting={isPersisting}
              language={language}
              onArchive={() => setIsArchiveConfirmationOpen(true)}
              onPublish={() => void publishForm()}
              onSaveDraft={() => void saveDraft()}
              saveState={saveState}
              savingLabel={t("form.designer.saving")}
              versionState={versionState}
            />
            <button
              className="secondary-button"
              disabled={isPersisting}
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

          <section className="designer-pages-panel" aria-label={pagingCopy.pages}>
            <div className="designer-pages-header">
              <div className="designer-pages-heading">
                <span className="eyebrow">{pagingCopy.pages}</span>
                <strong>{pagingCopy.pageDropHint}</strong>
              </div>
              <button className="secondary-button designer-page-add-button" type="button" onClick={addPage}>
                <FilePlus2 size={17} />
                {pagingCopy.addPage}
              </button>
            </div>

            <SortableContext
              items={pages.map((page) => getPageDragId(page.id))}
              strategy={horizontalListSortingStrategy}
            >
              <div className="designer-page-tabs" role="tablist" aria-label={pagingCopy.pages}>
                {pages.map((page, index) => {
                  const movedDirection = recentlyMovedPage?.id === page.id ? recentlyMovedPage.direction : null;
                  return (
                    <div
                      className={`designer-page-card-motion${
                        movedDirection === -1
                          ? " designer-page-card-moved-left"
                          : movedDirection === 1
                            ? " designer-page-card-moved-right"
                            : ""
                      }`}
                      key={page.id}
                      role="presentation"
                    >
                      <SortablePageTab
                        active={page.id === activePage?.id}
                        canMoveLeft={index > 0}
                        canMoveRight={index < pages.length - 1}
                        canRemove={pages.length > 1}
                        copy={pagingCopy}
                        hasError={!page.title.trim()}
                        index={index}
                        page={page}
                        onMove={(direction) => movePage(page.id, direction)}
                        onRemove={() => removePage(page.id)}
                        onSelect={() => {
                          setActivePageId(page.id);
                          setPaletteInsertIndex(null);
                        }}
                      />
                    </div>
                  );
                })}
              </div>
            </SortableContext>

            {activePage ? (
              <div className="designer-page-editor designer-page-metadata">
                <label>
                  {pagingCopy.pageTitle}
                  <input value={activePage.title} onChange={(event) => updatePage(activePage.id, { title: event.target.value })} />
                  {!activePage.title.trim() ? <span className="field-error">{pagingCopy.pageTitleRequired}</span> : null}
                </label>
                <label>
                  {pagingCopy.pageDescription}
                  <input
                    value={activePage.description}
                    onChange={(event) => updatePage(activePage.id, { description: event.target.value })}
                    placeholder={pagingCopy.pageDescriptionPlaceholder}
                  />
                </label>
              </div>
            ) : null}
          </section>

          <SortableContext items={activeFields.map((field) => field.id)} strategy={verticalListSortingStrategy}>
            <FieldCanvasDropZone label={t("form.designer.fieldListAria")}>
              <div className="designer-help-panel">
                <strong>{activePage?.title || t("form.designer.dropZoneTitle")}</strong>
                <span>
                  {t("form.designer.fieldListHelpDescription")} {pagingCopy.pageDropHint}
                </span>
              </div>
              {activeFields.length === 0 ? <p className="empty-state designer-page-empty">{pagingCopy.emptyPage}</p> : null}
              {activeFields.map((field, index) => (
                <Fragment key={field.id}>
                  {paletteInsertIndex === index ? (
                    <div className="field-insert-indicator field-insert-indicator-preview" aria-hidden="true" />
                  ) : null}
                  <FormFieldEditor
                    activeFieldCount={activeFields.length}
                    activePageId={activePage?.id ?? ""}
                    displacedFeedback={displacedFeedback}
                    field={field}
                    fields={fields}
                    highlighted={highlightedFieldId === field.id}
                    index={index}
                    language={language}
                    liveFieldError={fieldErrors[field.id]}
                    moveFeedback={moveFeedback}
                    pages={pages}
                    saveFieldError={saveFieldErrors[field.id]}
                    onAddOption={addOption}
                    onAddRule={addRequiredWhenRule}
                    onMoveField={moveField}
                    onMoveFieldToPage={moveFieldToPage}
                    onRemoveField={removeField}
                    onRemoveOption={removeOption}
                    onRemoveRule={removeRequiredWhenRule}
                    onToggleRequired={toggleRequired}
                    onUpdateField={updateField}
                    onUpdateFieldType={updateFieldType}
                    onUpdateOption={updateOption}
                    onUpdateRule={updateRequiredWhenRule}
                    onUpdateRuleDependency={updateRuleDependency}
                  />
                </Fragment>
              ))}
              {paletteInsertIndex === activeFields.length ? (
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
            <JsonViewer language={language} value={previewModel} />
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
              <FormVersionActionRail
                canArchive={Boolean(versionAdapter?.archive)}
                canPublish={Boolean(versionAdapter?.publish)}
                canSaveDraft={Boolean(versionAdapter?.saveDraft)}
                isPersisting={isPersisting}
                language={language}
                onArchive={() => setIsArchiveConfirmationOpen(true)}
                onPublish={() => void publishForm()}
                onSaveDraft={() => void saveDraft()}
                saveState={saveState}
                savingLabel={t("form.designer.saving")}
                versionState={versionState}
              />
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
              {showCommunityError ? <p className="field-error">{t("form.designer.communityRequired")}</p> : null}
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
        onSelect={(fieldType) => addFieldFromPalette(fieldType, activeFields.length)}
        openLabel={t("form.designer.mobilePaletteOpen")}
        title={t("form.designer.fieldPaletteTitle")}
      />
      {isArchiveConfirmationOpen ? (
        <ConfirmationDialog
          confirmLabel={pagingCopy.archiveConfirm}
          description={pagingCopy.archiveConfirmDescription}
          eyebrow={pagingCopy.archive}
          onCancel={() => setIsArchiveConfirmationOpen(false)}
          onConfirm={() => void archiveForm()}
          title={pagingCopy.archiveConfirmTitle}
        />
      ) : null}
    </section>
  );
}

function buildDesignerErrorSummary(
  fields: DesignerField[],
  errors: DesignerFieldErrors,
  language: Language,
  pageError?: string,
) {
  const messages: string[] = pageError ? [pageError] : [];

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
          filledOptions.findIndex(
            (candidate) => candidate.toLocaleLowerCase("tr") === option.toLocaleLowerCase("tr"),
          ) !== index,
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
  const remainingCount = Math.max(messages.length - visibleMessages.length, 0);
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

function mapApiErrorToDesignerFields(
  error: unknown,
  fields: DesignerField[],
  localizedMessage: string,
): DesignerFieldErrors {
  if (!(error instanceof ApiError)) {
    return {};
  }

  return error.errors.reduce<DesignerFieldErrors>((mappedErrors, rawMessage) => {
    const target = resolveApiFieldErrorTarget(rawMessage, fields);
    if (!target) {
      return mappedErrors;
    }

    if (!target.control) {
      return mappedErrors;
    }

    const current = mappedErrors[target.fieldId] ?? {};
    const next: DesignerFieldErrors[string] = { ...current };
    if (target.control === "key") {
      next.key = localizedMessage;
    } else if (target.control === "label") {
      next.label = localizedMessage;
    } else if (target.control === "options") {
      next.options = localizedMessage;
    } else if (target.control === "rules" && target.ruleIndex !== undefined) {
      next.rules = { ...next.rules, [target.ruleIndex]: localizedMessage };
    }

    mappedErrors[target.fieldId] = next;
    return mappedErrors;
  }, {});
}

function resolveApiFieldErrorTarget(message: string, fields: DesignerField[]) {
  const indexedPath = message.match(
    /\b(?:fields|fieldDefinitions|formFields)\s*(?:\[\s*(\d+)\s*\]|\.\s*(\d+))/i,
  );
  if (indexedPath) {
    const fieldIndex = Number(indexedPath[1] ?? indexedPath[2]);
    const field = fields[fieldIndex];
    if (!field) {
      return undefined;
    }

    const remainder = message.slice((indexedPath.index ?? 0) + indexedPath[0].length);
    const property = remainder.match(/^\s*\.\s*([a-zA-Z]+)/)?.[1]?.toLowerCase();
    const ruleIndexMatch = remainder.match(/validationRules\s*(?:\[\s*(\d+)\s*\]|\.\s*(\d+))/i);
    const ruleIndexValue = ruleIndexMatch?.[1] ?? ruleIndexMatch?.[2];

    return {
      fieldId: field.id,
      control: resolveApiFieldErrorControl(property),
      ruleIndex: ruleIndexValue === undefined ? undefined : Number(ruleIndexValue),
    };
  }

  const fieldIdMatch =
    message.match(/\bfieldId\s*[:=]\s*["']?([a-zA-Z0-9_-]+)["']?/i)
    ?? message.match(/\bfield\s+id\s*(?:[:=]|is)\s*["'`]([^"'`]+)["'`]/i);
  if (fieldIdMatch) {
    const field = fields.find((candidate) => candidate.id === fieldIdMatch[1]);
    if (field) {
      return { fieldId: field.id };
    }
  }

  const fieldKeyMatch =
    message.match(/\bfieldKey\s*[:=]\s*["']?([a-zA-Z0-9_.-]+)["']?/i)
    ?? message.match(/\bfield\s+key\s*(?:[:=]|is)\s*["'`]([^"'`]+)["'`]/i);
  if (!fieldKeyMatch) {
    return undefined;
  }

  const matchingFields = fields.filter(
    (field) => field.key.trim().toLocaleLowerCase("tr") === fieldKeyMatch[1].trim().toLocaleLowerCase("tr"),
  );
  return matchingFields.length === 1 ? { fieldId: matchingFields[0].id, control: "key" as const } : undefined;
}

function resolveApiFieldErrorControl(property?: string) {
  if (property === "key") return "key" as const;
  if (property === "label") return "label" as const;
  if (property === "options") return "options" as const;
  if (property === "validationrules" || property === "rules") return "rules" as const;
  return undefined;
}
