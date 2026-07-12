"use client";

import { GripVertical } from "lucide-react";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { ProcessCard } from "@/features/processes/ProcessCard";
import type { Language, ProcessSummary } from "@/lib/types";

type SortableProcessCardProps = {
  process: ProcessSummary;
  language: Language;
  isSelected: boolean;
  onSelect: (id: string) => void;
};

export function SortableProcessCard({ process, language, isSelected, onSelect }: SortableProcessCardProps) {
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

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={isDragging ? "process-sortable-item is-dragging" : "process-sortable-item"}
    >
      <button
        ref={setActivatorNodeRef}
        className="process-drag-handle"
        type="button"
        aria-label="Sırayı değiştirmek için sürükle"
        {...attributes}
        {...listeners}
      >
        <GripVertical size={16} aria-hidden="true" />
      </button>
      <ProcessCard
        process={process}
        language={language}
        isSelected={isSelected}
        onSelect={onSelect}
      />
    </div>
  );
}
