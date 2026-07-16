import type { DragEndEvent, DragOverEvent } from "@dnd-kit/core";
import { arrayMove } from "@dnd-kit/sortable";
import { createFieldKey, fieldTypeUsesOptions, supportedFieldTypes } from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import {
  resolveFormPages,
  type FormVersionStatus,
  type VersionedFormLayout,
} from "@/features/forms/formVersioning";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type {
  CreateFormRequest,
  FieldType,
  FormDefinition,
  FormFieldDefinition,
  Language,
  ValidationRule,
} from "@/lib/types";

export type DesignerField = Omit<FormFieldDefinition, "id"> & { id: string };

export type DesignerPage = {
  id: string;
  title: string;
  description: string;
  fields: DesignerField[];
};

export type DesignerVersionState = {
  versionId?: string;
  version: number;
  status: FormVersionStatus;
};

export const fieldPalettePrefix = "palette:";
export const pageDragPrefix = "designer-page:";
export const fieldCanvasDropId = "field-canvas";
export const fieldPaletteDropId = "field-palette-drop-zone";
export const paletteDragDistanceThreshold = 8;
const paletteEndInsertTolerance = 24;

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

export function createInitialPageId() {
  return "page-1";
}

export function getVersionStatusLabel(copy: ReturnType<typeof getFormPagingCopy>, status: FormVersionStatus) {
  if (status === "published") return copy.published;
  return status === "archived" ? copy.archived : copy.draft;
}

export function createInitialDesignerPages(language: Language): DesignerPage[] {
  const copy = getFormPagingCopy(language);
  return normalizeDesignerPages([
    {
      id: createInitialPageId(),
      title: `${copy.page} 1`,
      description: "",
      fields: initialFields.map((field) => ({
        ...field,
        options: [...field.options],
        validationRules: field.validationRules.map((rule) => ({ ...rule })),
      })),
    },
  ]);
}

export function flattenDesignerFields(pages: DesignerPage[]) {
  return pages.flatMap((page) => page.fields);
}

export function normalizeDesignerPages(pages: DesignerPage[]) {
  let sortOrder = 0;
  return pages.map((page) => ({
    ...page,
    fields: page.fields.map((field) => ({ ...field, sortOrder: (sortOrder += 1) })),
  }));
}

export function updateDesignerField(
  pages: DesignerPage[],
  fieldId: string,
  update: (field: DesignerField) => DesignerField,
) {
  return pages.map((page) => ({
    ...page,
    fields: page.fields.map((field) => (field.id === fieldId ? update(field) : field)),
  }));
}

export function findDesignerFieldLocation(pages: DesignerPage[], fieldId: string) {
  for (const [pageIndex, page] of pages.entries()) {
    const fieldIndex = page.fields.findIndex((field) => field.id === fieldId);
    if (fieldIndex >= 0) return { page, pageIndex, field: page.fields[fieldIndex], fieldIndex };
  }
  return undefined;
}

export function moveFieldWithinPage(pages: DesignerPage[], pageId: string, fieldId: string, direction: -1 | 1) {
  const page = pages.find((candidate) => candidate.id === pageId);
  if (!page) return pages;

  const currentIndex = page.fields.findIndex((field) => field.id === fieldId);
  const targetIndex = currentIndex + direction;
  if (currentIndex < 0 || targetIndex < 0 || targetIndex >= page.fields.length) return pages;

  return normalizeDesignerPages(
    pages.map((candidate) =>
      candidate.id === pageId ? { ...candidate, fields: arrayMove(candidate.fields, currentIndex, targetIndex) } : candidate,
    ),
  );
}

export function reorderFieldsInPage(pages: DesignerPage[], pageId: string, fieldId: string, overFieldId: string) {
  const page = pages.find((candidate) => candidate.id === pageId);
  if (!page) return pages;

  const currentIndex = page.fields.findIndex((field) => field.id === fieldId);
  const targetIndex = page.fields.findIndex((field) => field.id === overFieldId);
  if (currentIndex < 0 || targetIndex < 0) return pages;

  return normalizeDesignerPages(
    pages.map((candidate) =>
      candidate.id === pageId ? { ...candidate, fields: arrayMove(candidate.fields, currentIndex, targetIndex) } : candidate,
    ),
  );
}

export function moveFieldBetweenPages(pages: DesignerPage[], fieldId: string, destinationPageId: string) {
  const source = findDesignerFieldLocation(pages, fieldId);
  const destination = pages.find((page) => page.id === destinationPageId);
  if (!source || !destination || source.page.id === destinationPageId) return pages;

  return normalizeDesignerPages(
    pages.map((page) => {
      if (page.id === source.page.id) return { ...page, fields: page.fields.filter((field) => field.id !== fieldId) };
      if (page.id === destinationPageId) return { ...page, fields: [...page.fields, source.field] };
      return page;
    }),
  );
}

export function removeDesignerPage(pages: DesignerPage[], pageId: string, destinationPageId: string) {
  const removedPage = pages.find((page) => page.id === pageId);
  if (!removedPage || pages.length <= 1 || !pages.some((page) => page.id === destinationPageId)) return pages;

  return normalizeDesignerPages(
    pages
      .filter((page) => page.id !== pageId)
      .map((page) =>
        page.id === destinationPageId ? { ...page, fields: [...page.fields, ...removedPage.fields] } : page,
      ),
  );
}

export function reorderDesignerPages(pages: DesignerPage[], pageId: string, overPageId: string) {
  const currentIndex = pages.findIndex((page) => page.id === pageId);
  const targetIndex = pages.findIndex((page) => page.id === overPageId);
  if (currentIndex < 0 || targetIndex < 0) return pages;
  return normalizeDesignerPages(arrayMove(pages, currentIndex, targetIndex));
}

export function getPageDragId(pageId: string) {
  return `${pageDragPrefix}${pageId}`;
}

export function isPageDragId(id: DragEndEvent["active"]["id"]) {
  return String(id).startsWith(pageDragPrefix);
}

export function getPageIdFromDragId(id: DragEndEvent["active"]["id"]) {
  const value = String(id);
  return value.startsWith(pageDragPrefix) ? value.slice(pageDragPrefix.length) : "";
}

export function createVersionedLayout(
  pages: DesignerPage[],
  form: CreateFormRequest,
  versionState: DesignerVersionState,
): VersionedFormLayout {
  let fieldIndex = 0;
  return {
    ...(versionState.versionId ? { versionId: versionState.versionId } : {}),
    version: versionState.version,
    status: versionState.status,
    pages: pages.map((page) => ({
      id: page.id,
      title: page.title.trim(),
      description: page.description.trim(),
      fieldKeys: page.fields.map(() => form.fields[fieldIndex++]?.key ?? `field${fieldIndex}`),
    })),
  };
}

export function createDesignerFieldKey(value: string, fallbackIndex: number) {
  return createFieldKey(value, fallbackIndex);
}

export function isPaletteDragId(id: DragEndEvent["active"]["id"]) {
  return String(id).startsWith(fieldPalettePrefix);
}

export function hasPaletteDragDistance(delta: DragEndEvent["delta"]) {
  return Math.hypot(delta.x, delta.y) >= paletteDragDistanceThreshold;
}

export function resolvePaletteInsertIndex(
  event: DragOverEvent | DragEndEvent,
  fields: DesignerField[],
  palettePointerY: number | null = null,
) {
  const over = event.over;
  if (!over) return null;
  if (over.id === fieldCanvasDropId && fields.length === 0) return 0;

  const directlyOverField = fields.some((field) => field.id === over.id);
  const fieldCollision = directlyOverField
    ? null
    : event.collisions?.find((collision) => fields.some((field) => field.id === collision.id));
  const targetFieldId = directlyOverField ? over.id : fieldCollision?.id;
  const overFieldIndex = fields.findIndex((field) => field.id === targetFieldId);
  if (overFieldIndex < 0) {
    if (over.id !== fieldCanvasDropId || palettePointerY === null) return null;

    const lastField = fields[fields.length - 1];
    const lastFieldRect = lastField ? getDesignerFieldRect(lastField.id) : null;
    if (!lastFieldRect) return null;

    const isWithinEndTolerance =
      palettePointerY >= lastFieldRect.bottom &&
      palettePointerY <= lastFieldRect.bottom + paletteEndInsertTolerance;
    return isWithinEndTolerance ? fields.length : null;
  }

  const targetRect = directlyOverField ? over.rect : fieldCollision?.data?.droppableContainer?.rect.current;
  if (!targetRect) return null;

  const activeRect = event.active.rect.current.translated;
  const activeAnchorY = palettePointerY ?? (activeRect ? activeRect.top + Math.min(activeRect.height / 2, 28) : null);
  if (activeAnchorY === null) return null;

  const targetMiddleY = targetRect.top + targetRect.height / 2;
  return activeAnchorY > targetMiddleY ? overFieldIndex + 1 : overFieldIndex;
}

function getDesignerFieldRect(fieldId: string) {
  if (typeof document === "undefined") return null;

  const fieldElement = Array.from(document.querySelectorAll<HTMLElement>("[data-designer-field-id]")).find(
    (element) => element.dataset.designerFieldId === fieldId,
  );
  return fieldElement?.getBoundingClientRect() ?? null;
}

export function isSupportedFieldType(value: unknown): value is FieldType {
  return supportedFieldTypes.includes(value as FieldType);
}

export function getPaletteFieldDefaultLabel(language: Language, fieldType: FieldType) {
  return translate(language, `form.designer.fieldType${fieldType}Label` as TranslationKey);
}

export function toDesignerPages(form: FormDefinition, layout: VersionedFormLayout | null | undefined, pageLabel: string) {
  const resolved = resolveFormPages(form, layout, pageLabel);
  const usedFieldIds = new Set<string>();
  let fieldIndex = 0;
  const pages = resolved.pages.map((page) => ({
    id: page.id,
    title: page.title,
    description: page.description,
    fields: page.fields.map((field) => {
      const requestedId = field.id ?? `${field.key}-${fieldIndex}`;
      const id = createUniqueDesignerFieldId(requestedId, usedFieldIds);
      usedFieldIds.add(id);
      fieldIndex += 1;
      return {
        ...field,
        id,
        options: field.options ?? [],
        validationRules: field.validationRules ?? [],
      };
    }),
  }));

  return { layout: resolved.layout, pages: normalizeDesignerPages(pages) };
}

function createUniqueDesignerFieldId(requestedId: string, usedFieldIds: Set<string>) {
  if (!usedFieldIds.has(requestedId)) return requestedId;

  let suffix = 2;
  while (usedFieldIds.has(`${requestedId}-${suffix}`)) suffix += 1;
  return `${requestedId}-${suffix}`;
}

export function upsertForm(forms: FormDefinition[], form: FormDefinition) {
  const exists = forms.some((item) => item.id === form.id);
  return exists ? forms.map((item) => (item.id === form.id ? form : item)) : [form, ...forms];
}

export type DesignerFieldErrors = Record<string, { key?: string; label?: string; options?: string; rules?: Record<number, string> }>;

export function validateDesignerFields(fields: DesignerField[], language: Language) {
  const errors: DesignerFieldErrors = {};
  const keyCounts = fields.reduce<Record<string, number>>((current, field) => {
    if (!field.key.trim()) return current;
    const key = createDesignerFieldKey(field.key, field.sortOrder).toLowerCase();
    current[key] = (current[key] ?? 0) + 1;
    return current;
  }, {});

  for (const field of fields) {
    const fieldError: DesignerFieldErrors[string] = {};
    const key = createDesignerFieldKey(field.key, field.sortOrder);

    if (!field.key.trim()) fieldError.key = translate(language, "form.validation.fieldKeyRequired");
    else if (keyCounts[key.toLowerCase()] > 1) fieldError.key = translate(language, "form.validation.fieldKeyUnique");

    if (!field.label.trim()) fieldError.label = translate(language, "form.validation.labelRequired");

    if (fieldTypeUsesOptions(field.type)) {
      const filledOptions = field.options.map((option) => option.trim()).filter(Boolean);
      if (filledOptions.length === 0) fieldError.options = translate(language, "form.validation.optionsRequired");
      else if (field.options.some((option) => option.trim().length === 0)) {
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
      if (ruleError) fieldError.rules = { ...fieldError.rules, [ruleIndex]: ruleError };
    }

    if (Object.keys(fieldError).length > 0) errors[field.id] = fieldError;
  }

  return errors;
}

export function getDependencyCandidates(fields: DesignerField[], field: DesignerField) {
  return fields.filter((candidate) => candidate.id !== field.id && candidate.key.trim().length > 0);
}

export function findFirstDependencyField(fields: DesignerField[], field: DesignerField) {
  return getDependencyCandidates(fields, field)[0];
}

export function getDefaultExpectedValue(field: DesignerField) {
  if (field.type === "Checkbox") return "true";
  if (field.type === "Select" || field.type === "Radio") {
    return field.options.map((option) => option.trim()).find(Boolean) ?? "";
  }
  return "";
}

function validateRequiredWhenRule(
  field: DesignerField,
  rule: ValidationRule,
  dependency: DesignerField | undefined,
  language: Language,
) {
  if (rule.ruleType !== "RequiredWhen") return undefined;
  if (!rule.dependsOnFieldKey.trim()) return translate(language, "form.validation.dependencyRequired");
  if (!dependency) return translate(language, "form.validation.dependencyMustExist");
  if (dependency.id === field.id || dependency.key.trim() === field.key.trim()) {
    return translate(language, "form.validation.selfDependency");
  }
  if (!rule.expectedValue.trim()) return translate(language, "form.validation.expectedValueRequired");

  if (dependency.type === "Select" || dependency.type === "Radio") {
    const options = dependency.options.map((option) => option.trim()).filter(Boolean);
    if (!options.includes(rule.expectedValue.trim())) return translate(language, "form.validation.expectedSelectOption");
  }

  if (dependency.type === "Checkbox" && !["true", "false"].includes(rule.expectedValue.trim())) {
    return translate(language, "form.validation.expectedCheckbox");
  }

  return undefined;
}
