"use client";

import { ChevronDown, ChevronUp, GripVertical } from "lucide-react";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { ProcessCard } from "@/features/processes/ProcessCard";
import { translate } from "@/features/i18n/translations";
import type { Language, ProcessSummary } from "@/lib/types";

type SortableProcessCardProps = {
  process: ProcessSummary;
  language: Language;
  isSelected: boolean;
  isFirst: boolean;
  isLast: boolean;
  onSelect: (id: string) => void;
  onMoveUp: (id: string) => void;
  onMoveDown: (id: string) => void;
};

export function SortableProcessCard({
  process,
  language,
  isSelected,
  isFirst,
  isLast,
  onSelect,
  onMoveUp,
  onMoveDown,
}: SortableProcessCardProps) {
  const {
    attributes,
    listeners,
    setActivatorNodeRef,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: process.id });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const t = (key: Parameters<typeof translate>[1]) => translate(language, key);

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={isDragging ? "process-sortable-item is-dragging" : "process-sortable-item"}
    >
      <div className="process-move-controls">
        <button
          ref={setActivatorNodeRef}
          className="process-drag-handle"
          type="button"
          aria-label={t("process.dragHandle")}
          {...attributes}
          {...listeners}
        >
          <GripVertical size={16} aria-hidden="true" />
        </button>
        <button
          className="process-move-btn"
          type="button"
          disabled={isFirst}
          aria-label={t("process.moveUp")}
          onClick={() => onMoveUp(process.id)}
        >
          <ChevronUp size={14} aria-hidden="true" />
        </button>
        <button
          className="process-move-btn"
          type="button"
          disabled={isLast}
          aria-label={t("process.moveDown")}
          onClick={() => onMoveDown(process.id)}
        >
          <ChevronDown size={14} aria-hidden="true" />
        </button>
      </div>
      <ProcessCard
        process={process}
        language={language}
        isSelected={isSelected}
        onSelect={onSelect}
      />
    </div>
  );
}
