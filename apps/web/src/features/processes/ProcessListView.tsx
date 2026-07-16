"use client";

import { CircleDot, Filter } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import {
  closestCenter,
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { SortableProcessCard } from "@/features/processes/SortableProcessCard";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { applyStoredOrder } from "@/features/processes/processOrder";
import type { Language, ProcessStatus, ProcessSummary } from "@/lib/types";

type ProcessListViewProps = {
  processes: ProcessSummary[];
  language: Language;
  selectedProcessId: string;
  onSelectProcess: (id: string) => void;
};

type StatusFilter = "all" | ProcessStatus;

const filterOptions: { value: StatusFilter; labelKey: TranslationKey }[] = [
  { value: "all", labelKey: "process.filterAll" },
  { value: "Pending", labelKey: "process.filterPending" },
  { value: "InProgress", labelKey: "process.filterInProgress" },
  { value: "Completed", labelKey: "process.filterCompleted" },
  { value: "Rejected", labelKey: "process.filterRejected" },
  { value: "Escalated", labelKey: "process.filterEscalated" },
];

const STORAGE_KEY = "process-card-order";

export function ProcessListView({ processes, language, selectedProcessId, onSelectProcess }: ProcessListViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const [filter, setFilter] = useState<StatusFilter>("all");
  const [orderedProcesses, setOrderedProcesses] = useState<ProcessSummary[]>(processes);

  // API'den yeni liste geldiğinde kaydedilmiş sıralamayı koruyarak güncelle
  useEffect(() => {
    const raw = localStorage.getItem(STORAGE_KEY);
    const savedIds: string[] = raw ? (JSON.parse(raw) as string[]) : [];
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setOrderedProcesses(applyStoredOrder(processes, savedIds));
  }, [processes]);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    setOrderedProcesses((prev) => {
      const oldIndex = prev.findIndex((p) => p.id === active.id);
      const newIndex = prev.findIndex((p) => p.id === over.id);
      const next = arrayMove(prev, oldIndex, newIndex);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next.map((p) => p.id)));
      return next;
    });
  }

  function handleMoveUp(id: string) {
    setOrderedProcesses((prev) => {
      const index = prev.findIndex((p) => p.id === id);
      if (index <= 0) return prev;
      const next = arrayMove(prev, index, index - 1);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next.map((p) => p.id)));
      return next;
    });
  }

  function handleMoveDown(id: string) {
    setOrderedProcesses((prev) => {
      const index = prev.findIndex((p) => p.id === id);
      if (index < 0 || index >= prev.length - 1) return prev;
      const next = arrayMove(prev, index, index + 1);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next.map((p) => p.id)));
      return next;
    });
  }

  const filteredProcesses = useMemo(
    () => (filter === "all" ? orderedProcesses : orderedProcesses.filter((p) => p.status === filter)),
    [orderedProcesses, filter],
  );

  return (
    <article className="process-card">
      <div className="process-card-header">
        <div>
          <span className="eyebrow">{t("process.listEyebrow")}</span>
          <strong>{t("process.records", { visible: filteredProcesses.length, total: processes.length })}</strong>
        </div>
        <CircleDot size={22} />
      </div>

      <div className="status-filters">
        <Filter size={14} />
        {filterOptions.map((opt) => (
          <button
            className={filter === opt.value ? "filter-chip active" : "filter-chip"}
            key={opt.value}
            onClick={() => setFilter(opt.value)}
            type="button"
          >
            {t(opt.labelKey)}
          </button>
        ))}
      </div>

      {filteredProcesses.length === 0 ? (
        <p className="empty-state">
          {processes.length === 0 ? t("process.noProcess") : t("process.noFilterMatch")}
        </p>
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
          <SortableContext items={filteredProcesses.map((p) => p.id)} strategy={verticalListSortingStrategy}>
            <div className="process-list">
              {filteredProcesses.map((process, index) => (
                <SortableProcessCard
                  key={process.id}
                  isFirst={index === 0}
                  isLast={index === filteredProcesses.length - 1}
                  isSelected={process.id === selectedProcessId}
                  language={language}
                  process={process}
                  onSelect={onSelectProcess}
                  onMoveUp={handleMoveUp}
                  onMoveDown={handleMoveDown}
                />
              ))}
            </div>
          </SortableContext>

        </DndContext>
      )}
    </article>
  );
}
