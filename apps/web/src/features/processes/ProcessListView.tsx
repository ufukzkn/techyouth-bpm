"use client";

import { ArrowDown, ArrowUp, CircleDot, Filter } from "lucide-react";
import { useEffect, useState } from "react";
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
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { applyStoredOrder } from "@/features/processes/processOrder";
import { SortableProcessCard } from "@/features/processes/SortableProcessCard";
import type { Language, PagedResult, ProcessListParams, ProcessStatus, ProcessSummary } from "@/lib/types";

type StatusFilter = "all" | ProcessStatus;

type ProcessListViewProps = {
  cacheScope: string;
  language: Language;
  result: PagedResult<ProcessSummary>;
  selectedProcessId: string;
  sortBy: NonNullable<ProcessListParams["sortBy"]>;
  sortDirection: "asc" | "desc";
  statusFilter: StatusFilter;
  onNextPage: () => void;
  onPageChange: (page: number) => void;
  onPreviousPage: () => void;
  onSelectProcess: (id: string) => void;
  onSortByChange: (value: NonNullable<ProcessListParams["sortBy"]>) => void;
  onSortDirectionChange: (value: "asc" | "desc") => void;
  onStatusChange: (value: StatusFilter) => void;
};

const filterOptions: { value: StatusFilter; labelKey: TranslationKey }[] = [
  { value: "all", labelKey: "process.filterAll" },
  { value: "Pending", labelKey: "process.filterPending" },
  { value: "InProgress", labelKey: "process.filterInProgress" },
  { value: "Completed", labelKey: "process.filterCompleted" },
  { value: "Rejected", labelKey: "process.filterRejected" },
  { value: "Escalated", labelKey: "process.filterEscalated" },
];

export function ProcessListView({
  cacheScope,
  language,
  result,
  selectedProcessId,
  sortBy,
  sortDirection,
  statusFilter,
  onNextPage,
  onPageChange,
  onPreviousPage,
  onSelectProcess,
  onSortByChange,
  onSortDirectionChange,
  onStatusChange,
}: ProcessListViewProps) {
  const t = (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values);
  const isTr = language === "tr";
  const storageKey = `process-card-order:${cacheScope}`;
  const [orderedProcesses, setOrderedProcesses] = useState<ProcessSummary[]>(result.items);

  useEffect(() => {
    const raw = localStorage.getItem(storageKey);
    const savedIds: string[] = raw ? (JSON.parse(raw) as string[]) : [];
    // The server page changed; reconcile it once with the order persisted for this exact page key.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setOrderedProcesses(applyStoredOrder(result.items, savedIds));
  }, [result.items, storageKey]);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const totalPages = Math.max(1, Math.ceil(result.totalCount / result.pageSize));

  function persistOrder(next: ProcessSummary[]) {
    localStorage.setItem(storageKey, JSON.stringify(next.map((process) => process.id)));
    return next;
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    setOrderedProcesses((current) => {
      const oldIndex = current.findIndex((process) => process.id === active.id);
      const newIndex = current.findIndex((process) => process.id === over.id);
      return oldIndex < 0 || newIndex < 0 ? current : persistOrder(arrayMove(current, oldIndex, newIndex));
    });
  }

  function move(id: string, direction: -1 | 1) {
    setOrderedProcesses((current) => {
      const index = current.findIndex((process) => process.id === id);
      const target = index + direction;
      return index < 0 || target < 0 || target >= current.length
        ? current
        : persistOrder(arrayMove(current, index, target));
    });
  }

  return (
    <article className="process-card">
      <div className="process-card-header">
        <div>
          <span className="eyebrow">{t("process.listEyebrow")}</span>
          <strong>{t("process.records", { visible: orderedProcesses.length, total: result.totalCount })}</strong>
        </div>
        <CircleDot size={22} />
      </div>

      <div className="process-query-toolbar">
        <div className="status-filters">
          <Filter size={14} />
          {filterOptions.map((option) => (
            <button
              className={statusFilter === option.value ? "filter-chip active" : "filter-chip"}
              key={option.value}
              onClick={() => onStatusChange(option.value)}
              type="button"
            >
              {t(option.labelKey)}
            </button>
          ))}
        </div>
        <div className="process-sort-controls">
          <label>
            <span>{isTr ? "Sırala" : "Sort"}</span>
            <select value={sortBy} onChange={(event) => onSortByChange(event.target.value as NonNullable<ProcessListParams["sortBy"]>)}>
              <option value="dueAt">{isTr ? "En yakın son tarih" : "Nearest deadline"}</option>
              <option value="priority">{isTr ? "Öncelik" : "Priority"}</option>
              <option value="startedAt">{isTr ? "Başlangıç tarihi" : "Start date"}</option>
              <option value="status">{isTr ? "Durum" : "Status"}</option>
            </select>
          </label>
          <button
            aria-label={isTr ? "Sıralama yönünü değiştir" : "Change sort direction"}
            className="icon-button"
            onClick={() => onSortDirectionChange(sortDirection === "asc" ? "desc" : "asc")}
            title={sortDirection === "asc" ? (isTr ? "Artan" : "Ascending") : (isTr ? "Azalan" : "Descending")}
            type="button"
          >
            {sortDirection === "asc" ? <ArrowUp size={16} /> : <ArrowDown size={16} />}
          </button>
        </div>
      </div>

      {orderedProcesses.length === 0 ? (
        <p className="empty-state">{result.totalCount === 0 ? t("process.noProcess") : t("process.noFilterMatch")}</p>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={orderedProcesses.map((process) => process.id)} strategy={verticalListSortingStrategy}>
            <div className="process-list">
              {orderedProcesses.map((process, index) => (
                <SortableProcessCard
                  isFirst={index === 0}
                  isLast={index === orderedProcesses.length - 1}
                  isSelected={process.id === selectedProcessId}
                  key={process.id}
                  language={language}
                  onMoveDown={(id) => move(id, 1)}
                  onMoveUp={(id) => move(id, -1)}
                  onSelect={onSelectProcess}
                  process={process}
                />
              ))}
            </div>
          </SortableContext>
        </DndContext>
      )}

      {totalPages > 1 ? (
        <PaginationControls
          currentPage={result.page}
          language={language}
          onNext={onNextPage}
          onPageChange={onPageChange}
          onPrevious={onPreviousPage}
          totalPages={totalPages}
        />
      ) : null}
    </article>
  );
}
