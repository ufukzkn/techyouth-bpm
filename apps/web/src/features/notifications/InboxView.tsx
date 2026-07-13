"use client";

import { Bell, CheckCheck, Circle, Inbox, RefreshCw, Search } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { EmptyState } from "@/features/ui/EmptyState";
import { getNotificationTarget } from "@/features/notifications/notificationNavigation";
import { useNotificationStore } from "@/features/notifications/notificationStore";
import { api } from "@/lib/api";
import { formatApiDateTime } from "@/lib/dateTime";
import type {
  Language,
  NotificationCategory,
  NotificationItem,
  NotificationPage,
  NotificationReadStatus,
} from "@/lib/types";

const pageSize = 10;

export function InboxView({ language, token, userId }: { language: Language; token: string | null; userId: string }) {
  const router = useRouter();
  const [result, setResult] = useState<NotificationPage | null>(null);
  const [page, setPage] = useState(1);
  const [queryDraft, setQueryDraft] = useState("");
  const [query, setQuery] = useState("");
  const [readStatus, setReadStatus] = useState<NotificationReadStatus>("all");
  const [category, setCategory] = useState<NotificationCategory>("all");
  const [status, setStatus] = useState<"loading" | "refreshing" | "idle" | "error">("loading");
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const { loadPreview, markAllRead, setReadState } = useNotificationStore();
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  const load = useCallback(async (manual = false) => {
    if (!token || token.startsWith("demo-")) {
      setResult({ items: [], page: 1, pageSize, totalCount: 0, allCount: 0, unreadCount: 0 });
      setStatus("idle");
      return;
    }

    setStatus((current) => current === "idle" || current === "refreshing" ? "refreshing" : "loading");
    try {
      const next = await api.listNotifications(token, { page, pageSize, query, readStatus, category });
      setResult(next);
      setStatus("idle");
      if (manual) setToast({ kind: "success", text: t("inbox.refreshed") });
      await loadPreview(token, userId);
    } catch {
      setStatus("error");
      if (manual) setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }, [category, loadPreview, page, query, readStatus, t, token, userId]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function updateReadState(notification: NotificationItem, isRead: boolean) {
    if (!token || token.startsWith("demo-")) return;
    try {
      await setReadState(token, notification.id, isRead);
      await load();
      setToast({ kind: "success", text: t("inbox.updated") });
    } catch {
      setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }

  async function openNotification(notification: NotificationItem) {
    if (!notification.readAt) await updateReadState(notification, true);
    const target = getNotificationTarget(notification);
    if (target) router.push(target);
  }

  async function markEverythingRead() {
    if (!token || token.startsWith("demo-")) return;
    try {
      await markAllRead(token);
      setPage(1);
      await load();
      setToast({ kind: "success", text: t("inbox.updated") });
    } catch {
      setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setPage(1);
    setQuery(queryDraft.trim());
  }

  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / pageSize));
  const isInitialLoading = status === "loading" && !result;

  return (
    <div className="view-panel inbox-view">
      <section className="workspace-header inbox-header">
        <div>
          <span className="eyebrow">{t("inbox.eyebrow")}</span>
          <h1>{t("inbox.title")}</h1>
          <p>{t("inbox.description")}</p>
        </div>
        <div className="inbox-header-actions">
          <span className="inbox-unread-summary">{t("notifications.unreadCount", { count: result?.unreadCount ?? 0 })}</span>
          <button className="secondary-button" disabled={!result?.unreadCount} onClick={() => void markEverythingRead()} type="button">
            <CheckCheck size={17} /> {t("notifications.markAllRead")}
          </button>
          <button className="icon-button" aria-label={t("common.refresh")} onClick={() => void load(true)} type="button">
            <RefreshCw className={status === "refreshing" ? "spin-icon" : ""} size={17} />
          </button>
        </div>
      </section>

      <section className="inbox-toolbar" aria-label={t("inbox.title")}>
        <form className="inbox-search" onSubmit={submitSearch}>
          <Search size={17} aria-hidden="true" />
          <input value={queryDraft} onChange={(event) => setQueryDraft(event.target.value)} placeholder={t("inbox.search")} />
        </form>
        <div className="inbox-read-tabs" role="tablist">
          {(["all", "unread", "read"] as NotificationReadStatus[]).map((value) => (
            <button
              aria-selected={readStatus === value}
              className={readStatus === value ? "active" : ""}
              key={value}
              onClick={() => { setPage(1); setReadStatus(value); }}
              role="tab"
              type="button"
            >
              {t(`inbox.${value}` as TranslationKey)}
            </button>
          ))}
        </div>
        <select value={category} onChange={(event) => { setPage(1); setCategory(event.target.value as NotificationCategory); }}>
          <option value="all">{t("inbox.categoryAll")}</option>
          <option value="task">{t("inbox.categoryTask")}</option>
          <option value="process">{t("inbox.categoryProcess")}</option>
          <option value="access">{t("inbox.categoryAccess")}</option>
          <option value="account">{t("inbox.categoryAccount")}</option>
        </select>
      </section>

      <section className="inbox-list-panel">
        {isInitialLoading ? <InboxSkeleton /> : null}
        {!isInitialLoading && status === "error" && !result ? (
          <EmptyState description={t("inbox.loadError")} icon={<Bell size={20} />} title={t("api.error.generic")} />
        ) : null}
        {!isInitialLoading && result?.items.length === 0 ? (
          <EmptyState description={t("inbox.emptyDescription")} icon={<Inbox size={20} />} title={t("inbox.emptyTitle")} />
        ) : null}
        {result?.items.map((notification) => (
          <article className={notification.readAt ? "inbox-item" : "inbox-item is-unread"} key={notification.id}>
            <button className="inbox-item-main" onClick={() => void openNotification(notification)} type="button">
              <span className="inbox-item-state" aria-hidden="true"><Circle size={10} /></span>
              <span>
                <strong>{notification.title}</strong>
                <p>{notification.message}</p>
                <small>{formatApiDateTime(notification.createdAt, language)}</small>
              </span>
            </button>
            <button className="secondary-button inbox-read-action" onClick={() => void updateReadState(notification, !notification.readAt)} type="button">
              {notification.readAt ? t("inbox.markUnread") : t("inbox.markRead")}
            </button>
          </article>
        ))}
        {result && result.totalCount > pageSize ? (
          <PaginationControls
            currentPage={page}
            language={language}
            onNext={() => setPage((current) => Math.min(totalPages, current + 1))}
            onPageChange={setPage}
            onPrevious={() => setPage((current) => Math.max(1, current - 1))}
            totalPages={totalPages}
          />
        ) : null}
      </section>
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </div>
  );
}

function InboxSkeleton() {
  return (
    <div className="inbox-skeleton" aria-hidden="true">
      {[0, 1, 2, 3].map((item) => <SkeletonBlock className="inbox-skeleton-row" key={item} />)}
    </div>
  );
}
