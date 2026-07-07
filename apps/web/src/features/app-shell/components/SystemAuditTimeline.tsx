import { Search } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { buildAuditSearchText } from "@/features/app-shell/auditUtils";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { formatSessionExpiry } from "@/features/app-shell/sessionFormatters";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language, SystemAuditLog } from "@/lib/types";

export function SystemAuditTimeline({
  logs,
  language,
  emptyText,
  pageSize = 5,
  searchable = false,
}: {
  logs: SystemAuditLog[];
  language: Language;
  emptyText: string;
  pageSize?: number;
  searchable?: boolean;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [page, setPage] = useState(1);
  const [timelineQuery, setTimelineQuery] = useState("");
  const trimmedTimelineQuery = timelineQuery.trim().toLowerCase();
  const filteredLogs = useMemo(() => {
    if (!searchable || trimmedTimelineQuery.length < 2) {
      return logs;
    }

    return logs.filter((log) => buildAuditSearchText(log, language).includes(trimmedTimelineQuery));
  }, [language, logs, searchable, trimmedTimelineQuery]);
  const totalPages = Math.max(1, Math.ceil(filteredLogs.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleLogs = filteredLogs.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  if (logs.length === 0) {
    return (
      <div className="timeline-reveal">
        <p className="status-line">{emptyText}</p>
      </div>
    );
  }

  return (
    <div className="timeline-reveal">
      {searchable ? (
        <label className="search-field timeline-filter">
          <Search size={16} />
          <input
            value={timelineQuery}
            onChange={(event) => {
              setTimelineQuery(event.target.value);
              setPage(1);
            }}
            placeholder={t("logs.timelineSearchPlaceholder")}
          />
        </label>
      ) : null}
      <div className="system-audit-timeline">
        {visibleLogs.map((log) => (
          <article className="system-audit-event" key={log.id}>
            <span>{log.action}</span>
            <strong>{log.description}</strong>
            <small>
              {log.actorDisplayName} / {log.actorUsername} - {formatSessionExpiry(log.createdAt, language)}
            </small>
          </article>
        ))}
        {!visibleLogs.length ? <p className="status-line">{t("logs.noRelated")}</p> : null}
      </div>
      {filteredLogs.length > pageSize ? (
        <PaginationControls
          currentPage={currentPage}
          language={language}
          onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
          onPageChange={setPage}
          onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
          totalPages={totalPages}
        />
      ) : null}
    </div>
  );
}
