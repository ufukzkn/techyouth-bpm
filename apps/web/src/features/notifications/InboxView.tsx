"use client";

import { Bell, CheckCheck, Circle, Inbox, Mail, MailOpen, RefreshCw, Search } from "lucide-react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { ActionFeedback, SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { EmptyState } from "@/features/ui/EmptyState";
import { SlidingSegmentedControl } from "@/features/ui/SlidingSegmentedControl";
import { getNotificationTarget } from "@/features/notifications/notificationNavigation";
import { NotificationCategoryMenu } from "@/features/notifications/NotificationCategoryMenu";
import { useNotificationStore } from "@/features/notifications/notificationStore";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, NotificationCategory, NotificationItem, NotificationReadStatus } from "@/lib/types";

const pageSize = 10;

export function InboxView({ language, token, userId }: { language: Language; token: string | null; userId: string }) {
  const router = useRouter();
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const {
    inboxCategory,
    inboxPage,
    inboxQuery,
    inboxReadStatus,
    inboxResult,
    inboxStatus,
    loadInbox,
    loadPreview,
    markAllRead,
    pendingReadIds,
    setInboxFilters,
    setReadState,
  } = useNotificationStore();
  const [queryDraft, setQueryDraft] = useState(inboxQuery);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [showInitialSkeleton, setShowInitialSkeleton] = useState(false);
  const isInitialLoading = inboxStatus === "loading" && !inboxResult;

  useEffect(() => {
    if (!token || token.startsWith("demo-")) {
      return;
    }
    void loadInbox(token, userId).catch(() => undefined);
  }, [inboxCategory, inboxPage, inboxQuery, inboxReadStatus, loadInbox, token, userId]);

  useEffect(() => {
    if (!toast) {
      return;
    }
    const timer = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  useEffect(() => {
    const timer = window.setTimeout(
      () => setShowInitialSkeleton(isInitialLoading),
      isInitialLoading ? 500 : 0,
    );
    return () => window.clearTimeout(timer);
  }, [isInitialLoading]);

  async function updateReadState(notification: NotificationItem, isRead: boolean) {
    if (!token || token.startsWith("demo-")) {
      return;
    }
    try {
      await setReadState(token, notification.id, isRead);
      setToast({ kind: "success", text: t("inbox.updated") });
    } catch {
      setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }

  async function openNotification(notification: NotificationItem) {
    if (!notification.readAt) {
      await updateReadState(notification, true);
    }
    const target = getNotificationTarget(notification);
    if (target) {
      router.push(target);
    }
  }

  async function markEverythingRead() {
    if (!token || token.startsWith("demo-")) {
      return;
    }
    try {
      await markAllRead(token);
      setToast({ kind: "success", text: t("inbox.updated") });
    } catch {
      setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }

  async function refresh() {
    if (!token || token.startsWith("demo-")) {
      return;
    }
    try {
      await loadInbox(token, userId, { force: true });
      await loadPreview(token, userId, "background");
      setToast({ kind: "success", text: t("inbox.refreshed") });
    } catch {
      setToast({ kind: "error", text: t("inbox.loadError") });
    }
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setInboxFilters({ page: 1, query: queryDraft.trim() });
  }

  const totalPages = Math.max(1, Math.ceil((inboxResult?.totalCount ?? 0) / pageSize));
  const readOptions = (["all", "unread", "read"] as NotificationReadStatus[]).map((value) => ({
    value,
    label: t(`inbox.${value}` as TranslationKey),
  }));
  const categoryOptions = (["all", "task", "process", "access", "account"] as NotificationCategory[]).map((value) => ({
    value,
    label: t(`inbox.category${value === "all" ? "All" : value[0].toUpperCase() + value.slice(1)}` as TranslationKey),
  }));

  return (
    <div className="view-panel inbox-view">
      <section className="workspace-header inbox-header">
        <div>
          <span className="eyebrow">{t("inbox.eyebrow")}</span>
          <h1>{t("inbox.title")}</h1>
          <p>{t("inbox.description")}</p>
        </div>
        <div className="inbox-header-actions">
          <span className="inbox-unread-summary">{t("notifications.unreadCount", { count: inboxResult?.unreadCount ?? 0 })}</span>
          <button className="secondary-button" disabled={!inboxResult?.unreadCount} onClick={() => void markEverythingRead()} type="button">
            <CheckCheck size={17} /> {t("notifications.markAllRead")}
          </button>
          <button aria-label={t("common.refresh")} className="icon-button" onClick={() => void refresh()} type="button">
            <RefreshCw className={inboxStatus === "refreshing" ? "spin-icon" : ""} size={17} />
          </button>
        </div>
      </section>

      <section aria-label={t("inbox.title")} className="inbox-toolbar">
        <form className="inbox-search" onClick={() => document.getElementById("inbox-search-input")?.focus()} onSubmit={submitSearch}>
          <Search aria-hidden="true" size={17} />
          <input id="inbox-search-input" onChange={(event) => setQueryDraft(event.target.value)} placeholder={t("inbox.search")} value={queryDraft} />
        </form>
        <SlidingSegmentedControl
          ariaLabel={t("inbox.title")}
          name="inbox-read-status"
          onChange={(readStatus) => setInboxFilters({ page: 1, readStatus })}
          options={readOptions}
          value={inboxReadStatus}
        />
        <NotificationCategoryMenu
          label={t("inbox.title")}
          onChange={(category) => setInboxFilters({ category, page: 1 })}
          options={categoryOptions}
          value={inboxCategory}
        />
        <div className="inbox-toolbar-feedback"><ActionFeedback feedback={toast ? { tone: toast.kind, text: toast.text } : null} /></div>
      </section>

      <section className="inbox-list-panel">
        {inboxStatus === "refreshing" && inboxResult ? <div className="inbox-background-refresh"><span className="button-spinner" />{t("common.refreshing")}</div> : null}
        {showInitialSkeleton ? <InboxSkeleton /> : null}
        {!isInitialLoading && inboxStatus === "error" && !inboxResult ? <EmptyState description={t("inbox.loadError")} icon={<Bell size={20} />} title={t("api.error.generic")} /> : null}
        {!isInitialLoading && inboxResult?.items.length === 0 ? <EmptyState description={t("inbox.emptyDescription")} icon={<Inbox size={20} />} title={t("inbox.emptyTitle")} /> : null}
        {inboxResult?.items.map((notification) => {
          const isPending = Boolean(pendingReadIds[notification.id]);
          return (
            <article className={notification.readAt ? "inbox-item" : "inbox-item is-unread"} key={notification.id}>
              <button className="inbox-item-main" onClick={() => void openNotification(notification)} type="button">
                <span aria-hidden="true" className="inbox-item-state"><Circle size={10} /></span>
                <span><strong>{notification.title}</strong><p>{notification.message}</p><small>{formatApiDateTime(notification.createdAt, language)}</small></span>
              </button>
              <button
                aria-label={notification.readAt ? t("inbox.markUnread") : t("inbox.markRead")}
                className="icon-button inbox-read-action"
                disabled={isPending}
                onClick={() => void updateReadState(notification, !notification.readAt)}
                title={notification.readAt ? t("inbox.markUnread") : t("inbox.markRead")}
                type="button"
              >
                {isPending ? <span aria-hidden="true" className="button-spinner" /> : notification.readAt ? <Mail size={17} /> : <MailOpen size={17} />}
              </button>
            </article>
          );
        })}
        {inboxResult && inboxResult.totalCount > pageSize ? (
          <PaginationControls
            currentPage={inboxPage}
            language={language}
            onNext={() => setInboxFilters({ page: Math.min(totalPages, inboxPage + 1) })}
            onPageChange={(page) => setInboxFilters({ page })}
            onPrevious={() => setInboxFilters({ page: Math.max(1, inboxPage - 1) })}
            totalPages={totalPages}
          />
        ) : null}
      </section>
    </div>
  );
}

function InboxSkeleton() {
  return <div aria-hidden="true" className="inbox-skeleton">{[0, 1, 2, 3].map((item) => <SkeletonBlock className="inbox-skeleton-row" key={item} />)}</div>;
}
