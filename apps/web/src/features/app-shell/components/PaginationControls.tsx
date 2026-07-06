import { ChevronLeft, ChevronRight } from "lucide-react";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export function PaginationControls({
  currentPage,
  language,
  onNext,
  onPageChange,
  onPrevious,
  totalPages,
}: {
  currentPage: number;
  language: Language;
  onNext: () => void;
  onPageChange: (page: number) => void;
  onPrevious: () => void;
  totalPages: number;
}) {
  const safeTotalPages = Math.max(1, totalPages);

  function applyDraftPage(input: HTMLInputElement) {
    const requestedPage = Number.parseInt(input.value, 10);
    if (Number.isNaN(requestedPage)) {
      input.value = String(currentPage);
      return;
    }

    const nextPage = Math.min(Math.max(requestedPage, 1), safeTotalPages);
    input.value = String(nextPage);
    if (nextPage !== currentPage) {
      onPageChange(nextPage);
    }
  }

  return (
    <div className="pagination-controls">
      <button className="icon-button" type="button" disabled={currentPage <= 1} onClick={onPrevious}>
        <ChevronLeft size={16} />
      </button>
      <label className="pagination-jump">
        <span>{translate(language, "common.page", { current: currentPage, total: safeTotalPages })}</span>
        <input
          aria-label={translate(language, "common.pageJump")}
          defaultValue={currentPage}
          inputMode="numeric"
          key={currentPage}
          min={1}
          max={safeTotalPages}
          type="number"
          onBlur={(event) => applyDraftPage(event.currentTarget)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.currentTarget.blur();
            }
          }}
        />
      </label>
      <button className="icon-button" type="button" disabled={currentPage >= totalPages} onClick={onNext}>
        <ChevronRight size={16} />
      </button>
    </div>
  );
}
