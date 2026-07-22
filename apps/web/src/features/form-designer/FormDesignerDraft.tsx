"use client";

import {
  Download,
  Plus,
  Upload,
} from "lucide-react";
import dynamic from "next/dynamic";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { InlineValueLoader } from "@/features/app-shell/components/AsyncState";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import {
  FormPrimaryVersionActions,
  type FormSaveState,
} from "@/features/form-designer/FormVersionActions";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import {
  createDefaultField,
  createDefaultOptions,
  fieldTypeLabel,
  fieldTypeUsesOptions,
  supportedFieldTypes,
} from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import { parseFormDraft, serializeFormDraft } from "@/features/forms/formDraftTransfer";
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
  flattenDesignerFields,
  getDefaultExpectedValue,
  getPaletteFieldDefaultLabel,
  moveFieldBetweenPages,
  moveFieldWithinPage,
  normalizeDesignerPages,
  removeDesignerPage,
  reorderDesignerPages,
  reorderFieldsInPage,
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
import { downloadJsonDraft, readJsonDraftFile, toSafeFileName } from "@/lib/jsonDraftFile";
import type { Community, CreateFormRequest, FieldType, FormDefinition, Language, ValidationRule } from "@/lib/types";

type DesignerSaveFieldErrorSource = "client" | "api";
type DesignerSaveFieldError = DesignerFieldErrors[string] & { source: DesignerSaveFieldErrorSource };
type DesignerSaveFieldErrors = Record<string, DesignerSaveFieldError>;

const FormDesignerCanvas = dynamic(
  () => import("@/features/form-designer/FormDesignerCanvas").then((module) => module.FormDesignerCanvas),
  {
    ssr: false,
    loading: () => <FormDesignerCanvasSkeleton />,
  },
);

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
  const draftFileInputRef = useRef<HTMLInputElement>(null);
  const [saveState, setSaveState] = useState<FormSaveState>("idle");
  const [saveFieldErrors, setSaveFieldErrors] = useState<DesignerSaveFieldErrors>({});
  const [isArchiveConfirmationOpen, setIsArchiveConfirmationOpen] = useState(false);
  const [message, setMessage] = useState(() => t("form.designer.notSaved"));
  const [highlightedFieldId, setHighlightedFieldId] = useState("");
  const [moveFeedback, setMoveFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [displacedFeedback, setDisplacedFeedback] = useState<{ id: string; direction: -1 | 1 } | null>(null);
  const [recentlyMovedPage, setRecentlyMovedPage] = useState<{ id: string; direction: -1 | 1 } | null>(null);
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
    markUnsaved();
  }

  function movePage(id: string, direction: -1 | 1) {
    const currentIndex = pages.findIndex((page) => page.id === id);
    const targetIndex = currentIndex + direction;
    if (currentIndex < 0 || targetIndex < 0 || targetIndex >= pages.length) {
      return;
    }

    setPages((current) => reorderDesignerPages(current, id, current[targetIndex].id));
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
    triggerFieldHighlight(fieldId);
    markUnsaved();
    window.requestAnimationFrame(() => {
      document.getElementById(`designer-field-${fieldId}`)?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    });
  }

  function reorderPagesFromCanvas(sourcePageId: string, destinationPageId: string) {
    setPages((current) => reorderDesignerPages(current, sourcePageId, destinationPageId));
    markUnsaved();
  }

  function reorderFieldsFromCanvas(sourceFieldId: string, destinationFieldId: string) {
    setPages((current) => reorderFieldsInPage(current, activePageId, sourceFieldId, destinationFieldId));
    markUnsaved();
  }

  function selectPageFromCanvas(pageId: string) {
    setActivePageId(pageId);
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

  function exportDraft() {
    downloadJsonDraft(
      `${toSafeFileName(formName, "form-draft")}.techyouth-form.json`,
      serializeFormDraft({ name: formName, description, pages }),
    );
    setSaveState("success");
    setMessage(t("form.designer.draftExported"));
  }

  async function importDraft(file: File | undefined) {
    if (!file) return;

    try {
      const imported = parseFormDraft(await readJsonDraftFile(file));
      setSelectedFormId("");
      setFormName(imported.name);
      setDescription(imported.description);
      setPages(imported.pages);
      setActivePageId(imported.pages[0].id);
      setVersionState({ version: 1, status: "draft" });
      setSaveFieldErrors({});
      setShowCommunityError(false);
      setSaveState("success");
      setMessage(t("form.designer.draftImported"));
    } catch (error) {
      setSaveState("error");
      setMessage(formDraftImportError(error, t));
    } finally {
      if (draftFileInputRef.current) draftFileInputRef.current.value = "";
    }
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

      {isInitialDesignerLoading ? <FormDesignerCanvasSkeleton label={t("form.designer.loadingForms")} /> : null}

      {!isInitialDesignerLoading ? (
        <div className="designer-grid">
          <div className="tool-panel designer-form-info-panel" aria-busy={isSwitchingForm || isCreatingNewForm}>
            {isSwitchingForm || isCreatingNewForm ? (
              <div className="designer-form-transition-overlay" role="status" aria-live="polite">
                <span className="designer-form-transition-indicator"><InlineValueLoader label={isCreatingNewForm ? t("form.designer.preparingNewForm") : t("form.designer.loadingForms")} /></span>
              </div>
            ) : null}
            <h3>{t("form.designer.formInfo")}</h3>
            {isLoadingForms && !isSwitchingForm ? <div className="designer-loading-state" aria-live="polite"><InlineValueLoader label={t("form.designer.loadingForms")} /><span>{t("form.designer.loadingForms")}</span></div> : null}
            <label>{t("form.designer.savedForm")}<select disabled={isLoadingForms} value={selectedFormId} onChange={(event) => loadSavedForm(event.target.value)}><option value="">{isLoadingForms ? t("form.designer.loadingForms") : t("form.designer.newDraft")}</option>{savedForms.map((form) => <option key={form.id} value={form.id}>{form.name}{isSuperAdmin ? ` · ${form.communityName}` : ""}</option>)}</select></label>
            {isSuperAdmin ? <label>{t("form.designer.communityLabel")}<select disabled={isLoadingCommunities || Boolean(selectedFormId)} value={selectedCommunityId} onChange={(event) => { setSelectedCommunityId(event.target.value); setShowCommunityError(false); markUnsaved(); }}><option value="">{isLoadingCommunities ? t("form.designer.loadingCommunities") : t("form.designer.selectCommunity")}</option>{communities.filter((community) => community.isActive || community.id === selectedCommunityId).map((community) => <option key={community.id} value={community.id}>{community.name}{community.isActive ? "" : ` (${t("form.designer.inactiveCommunity")})`}</option>)}</select><span className="helper-copy">{selectedFormId ? t("form.designer.communityLocked") : t("form.designer.communityHelp")}</span>{showCommunityError ? <span className="field-error">{t("form.designer.communityRequired")}</span> : null}</label> : null}
            <label>{t("form.designer.formName")}<input value={formName} onChange={(event) => { setFormName(event.target.value); markUnsaved(); }} /></label>
            <label>{t("form.designer.descriptionLabel")}<input value={description} onChange={(event) => { setDescription(event.target.value); markUnsaved(); }} /></label>
            <p className="helper-copy">{selectedFormId ? t("form.designer.editingSelected", { name: selectedFormName ?? t("form.designer.selectedForm") }) : t("form.designer.createOnSave")}</p>
            <FormPrimaryVersionActions canArchive={Boolean(versionAdapter?.archive)} canPublish={Boolean(versionAdapter?.publish)} canSaveDraft={Boolean(versionAdapter?.saveDraft)} isPersisting={isPersisting} language={language} onArchive={() => setIsArchiveConfirmationOpen(true)} onPublish={() => void publishForm()} onSaveDraft={() => void saveDraft()} saveState={saveState} savingLabel={t("form.designer.saving")} versionState={versionState} />
            <button className="secondary-button" disabled={isPersisting} type="button" onClick={startNewForm}><Plus size={18} />{t("form.designer.newForm")}</button>
            <div className="designer-draft-transfer-actions"><button className="secondary-button" disabled={isPersisting} onClick={exportDraft} type="button"><Download size={17} aria-hidden="true" />{t("form.designer.exportDraft")}</button><button className="secondary-button" disabled={isPersisting} onClick={() => draftFileInputRef.current?.click()} type="button"><Upload size={17} aria-hidden="true" />{t("form.designer.importDraft")}</button><input ref={draftFileInputRef} accept="application/json,.json" className="draft-file-input" onChange={(event) => void importDraft(event.currentTarget.files?.[0])} type="file" /></div>
            <ol className="demo-steps" aria-label={t("form.designer.demoStepsAria")}><li>{t("form.designer.demoStepEdit")}</li><li>{t("form.designer.demoStepOptions")}</li><li>{t("form.designer.demoStepRequiredWhen")}</li><li>{t("form.designer.demoStepOrdering")}</li></ol>
          </div>

          <div className="tool-panel designer-manual-field-panel" aria-busy={isAddingManualField}>
            {isAddingManualField ? <div className="designer-manual-field-overlay" role="status" aria-live="polite"><span className="designer-manual-field-indicator"><InlineValueLoader label={t("form.designer.addingField")} /><span>{t("form.designer.addingField")}</span></span></div> : null}
            <h3>{t("form.designer.addFieldTitle")}</h3>
            <label>{t("form.designer.label")}<input value={label} onChange={(event) => setLabel(event.target.value)} placeholder={t("form.designer.labelPlaceholder")} /></label>
            <label>{t("form.designer.type")}<select value={type} onChange={(event) => setType(event.target.value as FieldType)}>{supportedFieldTypes.map((fieldType) => <option key={fieldType} value={fieldType}>{fieldTypeLabel(language, fieldType)}</option>)}</select></label>
            <label className="checkbox-row"><input checked={required} onChange={(event) => setRequired(event.target.checked)} type="checkbox" />{t("form.designer.requiredField")}</label>
            <button className="secondary-button" type="button" onClick={addField}><Plus size={18} />{t("form.designer.addField")}</button>
          </div>

          <FormDesignerCanvas
            activeFieldCount={activeFields.length}
            activeFields={activeFields}
            activePage={activePage}
            activePageId={activePageId}
            canArchive={Boolean(versionAdapter?.archive)}
            canPublish={Boolean(versionAdapter?.publish)}
            canSaveDraft={Boolean(versionAdapter?.saveDraft)}
            displacedFeedback={displacedFeedback}
            fieldErrorSummary={fieldErrorSummary}
            fieldErrors={fieldErrors}
            fields={fields}
            hasFieldErrors={hasFieldErrors}
            highlightedFieldId={highlightedFieldId}
            isPersisting={isPersisting}
            language={language}
            message={message}
            moveFeedback={moveFeedback}
            onAddOption={addOption}
            onAddPage={addPage}
            onAddPaletteField={addFieldFromPalette}
            onAddRule={addRequiredWhenRule}
            onArchive={() => setIsArchiveConfirmationOpen(true)}
            onMoveField={moveField}
            onMoveFieldToPage={moveFieldToPage}
            onMovePage={movePage}
            onPublish={() => void publishForm()}
            onRemoveField={removeField}
            onRemoveOption={removeOption}
            onRemovePage={removePage}
            onRemoveRule={removeRequiredWhenRule}
            onReorderFields={reorderFieldsFromCanvas}
            onReorderPages={reorderPagesFromCanvas}
            onSaveDraft={() => void saveDraft()}
            onSelectPage={selectPageFromCanvas}
            onToggleRequired={toggleRequired}
            onUpdateField={updateField}
            onUpdateFieldType={updateFieldType}
            onUpdateOption={updateOption}
            onUpdatePage={updatePage}
            onUpdateRule={updateRequiredWhenRule}
            onUpdateRuleDependency={updateRuleDependency}
            pages={pages}
            previewModel={previewModel}
            recentlyMovedPage={recentlyMovedPage}
            saveFieldErrors={saveFieldErrors}
            saveState={saveState}
            savingLabel={t("form.designer.saving")}
            showCommunityError={showCommunityError}
            t={t}
            versionState={versionState}
          />
        </div>
      ) : null}

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

function formDraftImportError(
  error: unknown,
  t: (key: TranslationKey, values?: Record<string, string | number>) => string,
) {
  if (error instanceof Error && error.message === "DRAFT_FILE_TOO_LARGE") {
    return t("form.designer.draftImportTooLarge");
  }
  if (error instanceof Error && error.message === "DRAFT_SCHEMA_UNSUPPORTED") {
    return t("form.designer.draftImportSchema");
  }
  return t("form.designer.draftImportInvalid");
}

function FormDesignerCanvasSkeleton({ label = "Loading form designer" }: { label?: string }) {
  return (
    <div className="designer-canvas-skeleton" role="status" aria-label={label}>
      <div className="designer-canvas-skeleton-heading"><InlineValueLoader label={label} /><span>{label}</span></div>
      <div className="designer-canvas-skeleton-page-tabs"><span /><span /><span /></div>
      <div className="designer-canvas-skeleton-fields"><span /><span /></div>
    </div>
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
