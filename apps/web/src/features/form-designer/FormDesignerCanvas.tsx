"use client";

import { FilePlus2 } from "lucide-react";
import { Fragment, useCallback, useEffect, useRef, useState } from "react";
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
import { horizontalListSortingStrategy, SortableContext, sortableKeyboardCoordinates, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { getEventCoordinates } from "@dnd-kit/utilities";
import { FormFieldEditor, type DesignerSaveFieldError } from "@/features/form-designer/FormFieldEditor";
import {
  FieldCanvasDropZone,
  FieldPaletteRail,
  PaletteFieldTypeCard,
  PaletteFieldTypeDragGhost,
  SortablePageTab,
  fieldTypeIcons,
} from "@/features/form-designer/FormDesignerComponents";
import { FormVersionActionRail, type FormSaveState } from "@/features/form-designer/FormVersionActions";
import { MobileFieldPalette } from "@/features/form-designer/MobileFieldPalette";
import {
  fieldCanvasDropId,
  getPageDragId,
  getPageIdFromDragId,
  isPageDragId,
  isPaletteDragId,
  isSupportedFieldType,
  paletteDragDistanceThreshold,
  resolvePaletteInsertIndex,
  type DesignerField,
  type DesignerFieldErrors,
  type DesignerPage,
  type DesignerVersionState,
} from "@/features/form-designer/formDesignerModel";
import { fieldTypeLabel, supportedFieldTypes } from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { JsonViewer } from "@/features/ui/JsonViewer";
import type { FieldType, Language, ValidationRule } from "@/lib/types";

type DirectionFeedback = { id: string; direction: -1 | 1 } | null;

type FieldErrorSummary = {
  messages: string[];
  remainingCount: number;
};

export type FormDesignerCanvasProps = {
  activeFieldCount: number;
  activeFields: DesignerField[];
  activePage?: DesignerPage;
  activePageId: string;
  canArchive: boolean;
  canPublish: boolean;
  canSaveDraft: boolean;
  displacedFeedback: DirectionFeedback;
  fieldErrorSummary: FieldErrorSummary;
  fieldErrors: DesignerFieldErrors;
  fields: DesignerField[];
  hasFieldErrors: boolean;
  highlightedFieldId: string;
  isPersisting: boolean;
  language: Language;
  message: string;
  moveFeedback: DirectionFeedback;
  onAddOption: (fieldId: string) => void;
  onAddPage: () => void;
  onAddPaletteField: (fieldType: FieldType, insertIndex: number) => void;
  onAddRule: (fieldId: string) => void;
  onArchive: () => void;
  onMoveField: (fieldId: string, direction: -1 | 1) => void;
  onMoveFieldToPage: (fieldId: string, destinationPageId: string) => void;
  onMovePage: (pageId: string, direction: -1 | 1) => void;
  onPublish: () => void;
  onRemoveField: (fieldId: string) => void;
  onRemoveOption: (fieldId: string, optionIndex: number) => void;
  onRemovePage: (pageId: string) => void;
  onRemoveRule: (fieldId: string, ruleIndex: number) => void;
  onReorderFields: (activeFieldId: string, overFieldId: string) => void;
  onReorderPages: (activePageId: string, overPageId: string) => void;
  onSaveDraft: () => void;
  onSelectPage: (pageId: string) => void;
  onToggleRequired: (fieldId: string) => void;
  onUpdateField: (fieldId: string, patch: Partial<Omit<DesignerField, "id">>) => void;
  onUpdateFieldType: (fieldId: string, type: FieldType) => void;
  onUpdateOption: (fieldId: string, optionIndex: number, value: string) => void;
  onUpdatePage: (pageId: string, patch: Partial<Pick<DesignerPage, "title" | "description">>) => void;
  onUpdateRule: (fieldId: string, ruleIndex: number, patch: Partial<ValidationRule>) => void;
  onUpdateRuleDependency: (fieldId: string, ruleIndex: number, dependencyKey: string) => void;
  pages: DesignerPage[];
  previewModel: unknown;
  recentlyMovedPage: DirectionFeedback;
  saveFieldErrors: Record<string, DesignerSaveFieldError>;
  saveState: FormSaveState;
  savingLabel: string;
  showCommunityError: boolean;
  t: (key: TranslationKey, values?: Record<string, string | number>) => string;
  versionState: DesignerVersionState;
};

export function FormDesignerCanvas(props: FormDesignerCanvasProps) {
  const {
    activeFields,
    activePage,
    canArchive,
    canPublish,
    canSaveDraft,
    displacedFeedback,
    fieldErrorSummary,
    fieldErrors,
    fields,
    hasFieldErrors,
    highlightedFieldId,
    isPersisting,
    language,
    message,
    moveFeedback,
    pages,
    previewModel,
    recentlyMovedPage,
    saveFieldErrors,
    saveState,
    savingLabel,
    showCommunityError,
    t,
    versionState,
  } = props;
  const [paletteInsertIndex, setPaletteInsertIndex] = useState<number | null>(null);
  const [paletteDragGhost, setPaletteDragGhost] = useState<{ fieldType: FieldType; x: number; y: number } | null>(null);
  const paletteGhostFrameRef = useRef<number | null>(null);
  const pendingPaletteGhostCoordinatesRef = useRef<{ x: number; y: number } | null>(null);
  const lastPalettePointerYRef = useRef<number | null>(null);
  const activePaletteGhostFieldType = paletteDragGhost?.fieldType ?? null;
  const pagingCopy = getFormPagingCopy(language);
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: paletteDragDistanceThreshold } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const paletteAwareCollisionDetection = useCallback<CollisionDetection>((args) => {
    if (!isPaletteDragId(args.active.id)) {
      const isExistingFieldDrag = fields.some((field) => field.id === args.active.id);
      if (!isExistingFieldDrag) return closestCenter(args);
      const validIds = new Set([...activeFields.map((field) => field.id), ...pages.map((page) => getPageDragId(page.id))]);
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
  }, [activeFields, fields, pages]);

  useEffect(() => {
    if (!activePaletteGhostFieldType) return;
    function handlePalettePointerMove(event: PointerEvent) {
      pendingPaletteGhostCoordinatesRef.current = { x: event.clientX, y: event.clientY };
      if (paletteGhostFrameRef.current !== null) return;
      paletteGhostFrameRef.current = window.requestAnimationFrame(() => {
        paletteGhostFrameRef.current = null;
        const coordinates = pendingPaletteGhostCoordinatesRef.current;
        if (!coordinates) return;
        setPaletteDragGhost((current) => current?.fieldType === activePaletteGhostFieldType ? { ...current, ...coordinates } : current);
      });
    }
    window.addEventListener("pointermove", handlePalettePointerMove, { passive: true });
    return () => {
      window.removeEventListener("pointermove", handlePalettePointerMove);
      if (paletteGhostFrameRef.current !== null) window.cancelAnimationFrame(paletteGhostFrameRef.current);
      paletteGhostFrameRef.current = null;
      pendingPaletteGhostCoordinatesRef.current = null;
    };
  }, [activePaletteGhostFieldType]);

  function clearPaletteDragState() {
    setPaletteDragGhost(null);
    setPaletteInsertIndex(null);
    lastPalettePointerYRef.current = null;
  }

  function handleDragStart(event: DragStartEvent) {
    if (!isPaletteDragId(event.active.id)) {
      setPaletteDragGhost(null);
      return;
    }
    const fieldType = event.active.data.current?.fieldType;
    const pointerCoordinates = getEventCoordinates(event.activatorEvent);
    setPaletteDragGhost(isSupportedFieldType(fieldType) && pointerCoordinates ? { fieldType, x: pointerCoordinates.x, y: pointerCoordinates.y } : null);
    setPaletteInsertIndex(null);
    lastPalettePointerYRef.current = null;
  }

  function handleDragOver(event: DragOverEvent) {
    if (isPaletteDragId(event.active.id)) {
      setPaletteInsertIndex(resolvePaletteInsertIndex(event, activeFields, lastPalettePointerYRef.current));
    }
  }

  function handleDragCancel(event: DragCancelEvent) {
    if (isPaletteDragId(event.active.id)) clearPaletteDragState();
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (isPaletteDragId(active.id)) {
      const fieldType = active.data.current?.fieldType;
      const hasValidDropTarget = Boolean(over && (over.id === fieldCanvasDropId || activeFields.some((field) => field.id === over.id)));
      if (paletteInsertIndex !== null && hasValidDropTarget && Math.hypot(event.delta.x, event.delta.y) >= paletteDragDistanceThreshold && isSupportedFieldType(fieldType)) {
        props.onAddPaletteField(fieldType, paletteInsertIndex);
      }
      clearPaletteDragState();
      return;
    }
    if (isPageDragId(active.id)) {
      if (over && isPageDragId(over.id) && active.id !== over.id) {
        props.onReorderPages(getPageIdFromDragId(active.id), getPageIdFromDragId(over.id));
      }
      return;
    }
    if (!over) return;
    const destinationPageId = getPageIdFromDragId(over.id);
    if (destinationPageId) {
      props.onMoveFieldToPage(String(active.id), destinationPageId);
      return;
    }
    if (active.id !== over.id && activeFields.some((field) => field.id === active.id) && activeFields.some((field) => field.id === over.id)) {
      props.onReorderFields(String(active.id), String(over.id));
    }
  }

  return (
    <>
      <DndContext sensors={sensors} collisionDetection={paletteAwareCollisionDetection} onDragCancel={handleDragCancel} onDragEnd={handleDragEnd} onDragOver={handleDragOver} onDragStart={handleDragStart}>
        <section className="designer-pages-panel" aria-label={pagingCopy.pages}>
          <div className="designer-pages-header">
            <div className="designer-pages-heading"><span className="eyebrow">{pagingCopy.pages}</span><strong>{pagingCopy.pageDropHint}</strong></div>
            <button className="secondary-button designer-page-add-button" type="button" onClick={props.onAddPage}><FilePlus2 size={17} />{pagingCopy.addPage}</button>
          </div>
          <SortableContext items={pages.map((page) => getPageDragId(page.id))} strategy={horizontalListSortingStrategy}>
            <div className="designer-page-tabs" role="tablist" aria-label={pagingCopy.pages}>
              {pages.map((page, index) => {
                const movedDirection = recentlyMovedPage?.id === page.id ? recentlyMovedPage.direction : null;
                return <div className={`designer-page-card-motion${movedDirection === -1 ? " designer-page-card-moved-left" : movedDirection === 1 ? " designer-page-card-moved-right" : ""}`} key={page.id} role="presentation">
                  <SortablePageTab active={page.id === activePage?.id} canMoveLeft={index > 0} canMoveRight={index < pages.length - 1} canRemove={pages.length > 1} copy={pagingCopy} hasError={!page.title.trim()} index={index} page={page} onMove={(direction) => props.onMovePage(page.id, direction)} onRemove={() => props.onRemovePage(page.id)} onSelect={() => props.onSelectPage(page.id)} />
                </div>;
              })}
            </div>
          </SortableContext>
          {activePage ? <div className="designer-page-editor designer-page-metadata">
            <label>{pagingCopy.pageTitle}<input value={activePage.title} onChange={(event) => props.onUpdatePage(activePage.id, { title: event.target.value })} />{!activePage.title.trim() ? <span className="field-error">{pagingCopy.pageTitleRequired}</span> : null}</label>
            <label>{pagingCopy.pageDescription}<input value={activePage.description} onChange={(event) => props.onUpdatePage(activePage.id, { description: event.target.value })} placeholder={pagingCopy.pageDescriptionPlaceholder} /></label>
          </div> : null}
        </section>

        <SortableContext items={activeFields.map((field) => field.id)} strategy={verticalListSortingStrategy}>
          <FieldCanvasDropZone label={t("form.designer.fieldListAria")}>
            <div className="designer-help-panel"><strong>{activePage?.title || t("form.designer.dropZoneTitle")}</strong><span>{t("form.designer.fieldListHelpDescription")} {pagingCopy.pageDropHint}</span></div>
            {activeFields.length === 0 ? <p className="empty-state designer-page-empty">{pagingCopy.emptyPage}</p> : null}
            {activeFields.map((field, index) => <Fragment key={field.id}>
              {paletteInsertIndex === index ? <div className="field-insert-indicator field-insert-indicator-preview" aria-hidden="true" /> : null}
              <FormFieldEditor activeFieldCount={activeFields.length} activePageId={activePage?.id ?? ""} displacedFeedback={displacedFeedback} field={field} fields={fields} highlighted={highlightedFieldId === field.id} index={index} language={language} liveFieldError={fieldErrors[field.id]} moveFeedback={moveFeedback} pages={pages} saveFieldError={saveFieldErrors[field.id]} onAddOption={props.onAddOption} onAddRule={props.onAddRule} onMoveField={props.onMoveField} onMoveFieldToPage={props.onMoveFieldToPage} onRemoveField={props.onRemoveField} onRemoveOption={props.onRemoveOption} onRemoveRule={props.onRemoveRule} onToggleRequired={props.onToggleRequired} onUpdateField={props.onUpdateField} onUpdateFieldType={props.onUpdateFieldType} onUpdateOption={props.onUpdateOption} onUpdateRule={props.onUpdateRule} onUpdateRuleDependency={props.onUpdateRuleDependency} />
            </Fragment>)}
            {paletteInsertIndex === activeFields.length ? <div className="field-insert-indicator field-insert-indicator-preview" aria-hidden="true" /> : null}
          </FieldCanvasDropZone>
        </SortableContext>

        <div className="json-preview-panel"><div><span className="eyebrow">{t("form.designer.jsonPreviewEyebrow")}</span><h3>{t("form.designer.jsonPreviewTitle")}</h3><p>{t("form.designer.jsonPreviewDescription")}</p></div><JsonViewer language={language} value={previewModel} /></div>
        <FieldPaletteRail label={t("form.designer.fieldPaletteStickyTitle")}>
          <div className="field-palette"><div className="field-palette-header"><strong>{t("form.designer.fieldPaletteStickyTitle")}</strong><span>{t("form.designer.fieldPaletteStickyDescription")}</span></div><div className="field-palette-grid">{supportedFieldTypes.map((fieldType) => <PaletteFieldTypeCard fieldType={fieldType} key={fieldType} language={language} />)}</div></div>
          <div className="designer-save-panel">
            <FormVersionActionRail canArchive={canArchive} canPublish={canPublish} canSaveDraft={canSaveDraft} isPersisting={isPersisting} language={language} onArchive={props.onArchive} onPublish={props.onPublish} onSaveDraft={props.onSaveDraft} saveState={saveState} savingLabel={savingLabel} versionState={versionState} />
            {hasFieldErrors ? <div className="field-error designer-blocking-error" role="alert"><strong>{t("form.designer.saveBlockedTitle")}</strong>{fieldErrorSummary.messages.length === 1 ? <span>{fieldErrorSummary.messages[0]}</span> : <ul>{fieldErrorSummary.messages.map((errorMessage) => <li key={errorMessage}>{errorMessage}</li>)}</ul>}{fieldErrorSummary.remainingCount > 0 ? <span className="designer-blocking-error-more">{t("form.designer.moreErrors", { count: fieldErrorSummary.remainingCount })}</span> : null}</div> : null}
            {showCommunityError ? <p className="field-error">{t("form.designer.communityRequired")}</p> : null}
            <p className={`status-line status-line-${saveState}`} aria-live="polite">{message}</p>
          </div>
        </FieldPaletteRail>
      </DndContext>
      {paletteDragGhost && typeof document !== "undefined" && document.body ? createPortal(<div className="field-palette-drag-ghost" style={{ left: paletteDragGhost.x, top: paletteDragGhost.y }} aria-hidden="true"><PaletteFieldTypeDragGhost fieldType={paletteDragGhost.fieldType} language={language} /></div>, document.body) : null}
      <MobileFieldPalette closeLabel={t("common.close")} description={t("form.designer.mobilePaletteDescription")} items={supportedFieldTypes.map((fieldType) => ({ type: fieldType, label: fieldTypeLabel(language, fieldType), description: translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey), icon: fieldTypeIcons[fieldType] }))} onSelect={(fieldType) => props.onAddPaletteField(fieldType, activeFields.length)} openLabel={t("form.designer.mobilePaletteOpen")} title={t("form.designer.fieldPaletteTitle")} />
    </>
  );
}
