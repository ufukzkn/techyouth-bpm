"use client";

import {
  AlignLeft,
  Calendar,
  ChevronLeft,
  ChevronRight,
  CircleDot,
  GripVertical,
  Hash,
  List,
  Mail,
  SquareCheck,
  Trash2,
  Type,
  type LucideIcon,
} from "lucide-react";
import type { ReactNode } from "react";
import { useDraggable, useDroppable } from "@dnd-kit/core";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { InlineValueLoader, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import {
  fieldCanvasDropId,
  fieldPaletteDropId,
  fieldPalettePrefix,
  getPageDragId,
  type DesignerField,
  type DesignerPage,
} from "@/features/form-designer/formDesignerModel";
import { fieldTypeLabel } from "@/features/forms/fieldTypes";
import { getFormPagingCopy } from "@/features/forms/formPagingCopy";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { FieldType, Language } from "@/lib/types";

export const fieldTypeIcons: Record<FieldType, LucideIcon> = {
  Text: Type,
  TextArea: AlignLeft,
  Number: Hash,
  Email: Mail,
  Select: List,
  Radio: CircleDot,
  Checkbox: SquareCheck,
  Date: Calendar,
};

export function FormDesignerOpeningSkeleton({ label }: { label: string }) {
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

export function SortablePageTab({
  page,
  index,
  active,
  canMoveLeft,
  canMoveRight,
  canRemove,
  hasError,
  copy,
  onSelect,
  onMove,
  onRemove,
}: {
  page: DesignerPage;
  index: number;
  active: boolean;
  canMoveLeft: boolean;
  canMoveRight: boolean;
  canRemove: boolean;
  hasError: boolean;
  copy: ReturnType<typeof getFormPagingCopy>;
  onSelect: () => void;
  onMove: (direction: -1 | 1) => void;
  onRemove: () => void;
}) {
  const { attributes, listeners, setActivatorNodeRef, setNodeRef, transform, transition, isDragging, isOver } = useSortable({
    id: getPageDragId(page.id),
    data: { kind: "page", pageId: page.id },
    transition: { duration: 260, easing: "cubic-bezier(0.2, 0.8, 0.2, 1)" },
  });

  return (
    <div
      ref={setNodeRef}
      className={`designer-page-tab${active ? " designer-page-tab-active" : ""}${
        isDragging ? " designer-page-tab-dragging" : ""
      }${isOver ? " designer-page-tab-drop-active" : ""}${hasError ? " designer-page-tab-error" : ""}`}
      style={{ transform: CSS.Transform.toString(transform), transition }}
    >
      <button
        ref={setActivatorNodeRef}
        className="designer-page-drag-handle"
        type="button"
        title={copy.dragPage}
        aria-label={`${copy.dragPage}: ${page.title}`}
        {...attributes}
        {...listeners}
      >
        <GripVertical size={16} />
      </button>
      <button className="designer-page-tab-main" type="button" role="tab" aria-selected={active} onClick={onSelect}>
        <span>{copy.page} {index + 1}</span>
        <strong>{page.title || `${copy.page} ${index + 1}`}</strong>
        <small>{page.fields.length} {copy.fields}</small>
      </button>
      <div className="designer-page-tab-actions">
        <button className="icon-button" disabled={!canMoveLeft} type="button" title={copy.movePageUp} aria-label={`${copy.movePageUp}: ${page.title}`} onClick={() => onMove(-1)}>
          <ChevronLeft size={15} />
        </button>
        <button className="icon-button" disabled={!canMoveRight} type="button" title={copy.movePageDown} aria-label={`${copy.movePageDown}: ${page.title}`} onClick={() => onMove(1)}>
          <ChevronRight size={15} />
        </button>
        <button className="icon-button" disabled={!canRemove} type="button" title={copy.removePage} aria-label={`${copy.removePage}: ${page.title}`} onClick={onRemove}>
          <Trash2 size={15} />
        </button>
      </div>
    </div>
  );
}

export function PaletteFieldTypeCard({ fieldType, language }: { fieldType: FieldType; language: Language }) {
  const label = fieldTypeLabel(language, fieldType);
  const description = translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey);
  const FieldTypeIcon = fieldTypeIcons[fieldType];
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `${fieldPalettePrefix}${fieldType}`,
    data: { fieldType },
  });

  return (
    <div
      ref={setNodeRef}
      className={`field-palette-item${isDragging ? " field-palette-item-dragging" : ""}`}
      style={isDragging ? undefined : { transform: CSS.Translate.toString(transform) }}
      aria-label={translate(language, "form.designer.dragFieldType", { type: label })}
      {...attributes}
      {...listeners}
    >
      <span className="field-palette-icon" aria-hidden="true"><FieldTypeIcon size={18} /></span>
      <span className="field-palette-item-copy"><strong>{label}</strong><span>{description}</span></span>
      <GripVertical className="field-palette-grip" size={16} aria-hidden="true" />
    </div>
  );
}

export function PaletteFieldTypeDragGhost({ fieldType, language }: { fieldType: FieldType; language: Language }) {
  const label = fieldTypeLabel(language, fieldType);
  const description = translate(language, `form.designer.fieldType${fieldType}Description` as TranslationKey);
  const FieldTypeIcon = fieldTypeIcons[fieldType];

  return (
    <div className="field-palette-item field-palette-drag-ghost-card">
      <span className="field-palette-icon" aria-hidden="true"><FieldTypeIcon size={18} /></span>
      <span className="field-palette-item-copy"><strong>{label}</strong><span>{description}</span></span>
      <GripVertical className="field-palette-grip" size={16} aria-hidden="true" />
    </div>
  );
}

export function FieldCanvasDropZone({ children, label }: { children: ReactNode; label: string }) {
  const { isOver, setNodeRef } = useDroppable({ id: fieldCanvasDropId });
  return <div id={fieldCanvasDropId} ref={setNodeRef} className={`field-list${isOver ? " field-list-drop-active" : ""}`} aria-label={label}>{children}</div>;
}

export function FieldPaletteRail({ children, label }: { children: ReactNode; label: string }) {
  const { isOver, setNodeRef } = useDroppable({ id: fieldPaletteDropId });
  return <aside ref={setNodeRef} className={`field-palette-rail${isOver ? " field-palette-rail-drop-active" : ""}`} aria-label={label}>{children}</aside>;
}

type SortableFieldCardRenderProps = Pick<ReturnType<typeof useSortable>, "attributes" | "listeners" | "setActivatorNodeRef" | "isDragging">;

function getSortableFieldCardTransform(transform: ReturnType<typeof useSortable>["transform"]) {
  if (!transform) return undefined;
  return CSS.Transform.toString({ ...transform, scaleX: 1, scaleY: 1 });
}

export function SortableFieldCard({ id, pageId, children }: { id: string; pageId: string; children: (props: SortableFieldCardRenderProps) => ReactNode }) {
  const { attributes, listeners, setActivatorNodeRef, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    data: { kind: "field", pageId },
    transition: { duration: 300, easing: "cubic-bezier(0.2, 0.8, 0.2, 1)" },
  });
  return (
    <div
      ref={setNodeRef}
      data-designer-field-id={id}
      className={`sortable-field-card${isDragging ? " sortable-field-card-dragging" : ""}`}
      style={{ transform: getSortableFieldCardTransform(transform), transition }}
    >
      {children({ attributes, listeners, setActivatorNodeRef, isDragging })}
    </div>
  );
}

export function ExpectedValueInput({
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
          {options.map((option) => <option key={option} value={option}>{option}</option>)}
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
      <input value={expectedValue} onChange={(event) => onChange(event.target.value)} placeholder={translate(language, "form.designer.expectedValuePlaceholder")} />
    </label>
  );
}
