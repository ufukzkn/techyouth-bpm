"use client";

import { create } from "zustand";
import { api } from "@/lib/api";
import type {
  NotificationCategory,
  NotificationItem,
  NotificationListParams,
  NotificationPage,
  NotificationReadStatus,
} from "@/lib/types";

const cacheTtlMs = 30_000;
const maxCachedPages = 30;

type CachedNotificationPage = {
  data: NotificationPage;
  params: Required<Pick<NotificationListParams, "page" | "pageSize">> & NotificationListParams;
  cachedAt: number;
};

type NotificationToast = {
  id: string;
  notification: NotificationItem;
};

type PreviewLoadSource = "initial" | "poll" | "visibility" | "popover" | "background";
type InboxStatus = "idle" | "loading" | "refreshing" | "error";

type NotificationState = {
  userId: string | null;
  previewItems: NotificationItem[];
  allCount: number;
  unreadCount: number;
  isLoading: boolean;
  isPreviewRefreshing: boolean;
  pendingReadIds: Record<string, true>;
  inboxResult: NotificationPage | null;
  inboxStatus: InboxStatus;
  inboxPage: number;
  inboxQuery: string;
  inboxReadStatus: NotificationReadStatus;
  inboxCategory: NotificationCategory;
  liveToasts: NotificationToast[];
  loadPreview: (token: string, userId: string, source?: PreviewLoadSource) => Promise<void>;
  loadInbox: (token: string, userId: string, options?: { force?: boolean }) => Promise<void>;
  setInboxFilters: (filters: Partial<{
    page: number;
    query: string;
    readStatus: NotificationReadStatus;
    category: NotificationCategory;
  }>) => void;
  setReadState: (token: string, notificationId: string, isRead: boolean) => Promise<void>;
  markAllRead: (token: string) => Promise<void>;
  dismissLiveToast: (toastId: string) => void;
  reset: (userId?: string | null) => void;
};

const pageCache = new Map<string, CachedNotificationPage>();
let knownPreviewIds = new Set<string>();
let hasPreviewBaseline = false;

function normalizedParams(state: NotificationState) {
  return {
    page: state.inboxPage,
    pageSize: 10,
    query: state.inboxQuery,
    readStatus: state.inboxReadStatus,
    category: state.inboxCategory,
  } satisfies NotificationListParams;
}

function cacheKey(userId: string, params: NotificationListParams) {
  return [
    userId,
    params.page ?? 1,
    params.pageSize ?? 10,
    params.query?.trim().toLowerCase() ?? "",
    params.readStatus ?? "all",
    params.category ?? "all",
  ].join("|");
}

function cachePage(key: string, entry: CachedNotificationPage) {
  pageCache.delete(key);
  pageCache.set(key, entry);
  while (pageCache.size > maxCachedPages) {
    const oldestKey = pageCache.keys().next().value;
    if (typeof oldestKey !== "string") {
      break;
    }
    pageCache.delete(oldestKey);
  }
}

function clearCache() {
  pageCache.clear();
}

function resetTracking() {
  knownPreviewIds = new Set<string>();
  hasPreviewBaseline = false;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  userId: null,
  previewItems: [],
  allCount: 0,
  unreadCount: 0,
  isLoading: false,
  isPreviewRefreshing: false,
  pendingReadIds: {},
  inboxResult: null,
  inboxStatus: "idle",
  inboxPage: 1,
  inboxQuery: "",
  inboxReadStatus: "all",
  inboxCategory: "all",
  liveToasts: [],

  loadPreview: async (token, userId, source = "background") => {
    if (get().userId !== userId) {
      clearCache();
      resetTracking();
      set({
        userId,
        previewItems: [],
        allCount: 0,
        unreadCount: 0,
        inboxResult: null,
        inboxStatus: "idle",
        liveToasts: [],
      });
    }

    const hasLoadedBaseline = hasPreviewBaseline;
    set(hasLoadedBaseline
      ? { isLoading: false, isPreviewRefreshing: true }
      : { isLoading: true, isPreviewRefreshing: false });
    try {
      const page = await api.listNotifications(token, { page: 1, pageSize: 5 });
      if (get().userId !== userId) {
        return;
      }

      const newUnread = page.items.filter((item) => !item.readAt && !knownPreviewIds.has(item.id));
      const shouldToast = hasPreviewBaseline && source === "poll" && document.visibilityState === "visible";
      const queuedToasts = shouldToast
        ? [...get().liveToasts, ...newUnread.map((notification) => ({ id: notification.id, notification }))].slice(-3)
        : get().liveToasts;

      page.items.forEach((item) => knownPreviewIds.add(item.id));
      hasPreviewBaseline = true;
      set({
        previewItems: page.items,
        allCount: page.allCount,
        unreadCount: page.unreadCount,
        isLoading: false,
        isPreviewRefreshing: false,
        liveToasts: queuedToasts,
      });
    } catch (error) {
      set({ isLoading: false, isPreviewRefreshing: false });
      throw error;
    }
  },

  loadInbox: async (token, userId, options = {}) => {
    if (get().userId !== userId) {
      get().reset(userId);
    }

    const params = normalizedParams(get());
    const key = cacheKey(userId, params);
    const cached = pageCache.get(key);
    const isFresh = cached && Date.now() - cached.cachedAt < cacheTtlMs;
    if (cached) {
      cachePage(key, cached);
      set({ inboxResult: cached.data, inboxStatus: isFresh && !options.force ? "idle" : "refreshing" });
      if (isFresh && !options.force) {
        return;
      }
    } else {
      const visibleResult = get().inboxResult;
      set({ inboxResult: visibleResult, inboxStatus: visibleResult ? "refreshing" : "loading" });
    }

    try {
      const result = await api.listNotifications(token, params);
      if (get().userId !== userId || key !== cacheKey(userId, normalizedParams(get()))) {
        return;
      }
      cachePage(key, { data: result, params: { ...params, page: params.page ?? 1, pageSize: params.pageSize ?? 10 }, cachedAt: Date.now() });
      const isDefaultView = params.page === 1
        && (!params.query || params.query === "")
        && (!params.readStatus || params.readStatus === "all")
        && (!params.category || params.category === "all");
      set({
        inboxResult: result,
        inboxStatus: "idle",
        allCount: result.allCount,
        unreadCount: result.unreadCount,
        ...(isDefaultView ? { previewItems: result.items.slice(0, 5) } : {}),
      });
    } catch (error) {
      set({ inboxStatus: cached || get().inboxResult ? "idle" : "error" });
      throw error;
    }
  },

  setInboxFilters: (filters) => set((state) => {
    const next = {
      page: filters.page ?? state.inboxPage,
      query: filters.query ?? state.inboxQuery,
      readStatus: filters.readStatus ?? state.inboxReadStatus,
      category: filters.category ?? state.inboxCategory,
    };
    const cached = state.userId
      ? pageCache.get(cacheKey(state.userId, { ...next, pageSize: 10 }))
      : undefined;
    return {
      inboxPage: next.page,
      inboxQuery: next.query,
      inboxReadStatus: next.readStatus,
      inboxCategory: next.category,
      // Keep the previous page visible while an uncached filter result is loading.
      inboxResult: cached?.data ?? state.inboxResult,
      inboxStatus: cached || state.inboxResult ? "refreshing" : "loading",
    };
  }),

  setReadState: async (token, notificationId, isRead) => {
    const current = get();
    if (current.pendingReadIds[notificationId]) {
      return;
    }

    const cacheSnapshot = new Map(pageCache);
    const stateSnapshot = {
      previewItems: current.previewItems,
      unreadCount: current.unreadCount,
      inboxResult: current.inboxResult,
    };
    const item = findNotification(notificationId, current.previewItems, current.inboxResult);
    const wasRead = Boolean(item?.readAt);
    if (item && wasRead === isRead) {
      return;
    }

    const changedAt = isRead ? new Date().toISOString() : null;
    updateCachedNotification(notificationId, changedAt);
    const nextUnreadCount = item ? Math.max(0, current.unreadCount + (isRead ? -1 : 1)) : current.unreadCount;
    set({
      pendingReadIds: { ...current.pendingReadIds, [notificationId]: true },
      previewItems: updateNotificationList(current.previewItems, notificationId, changedAt),
      unreadCount: nextUnreadCount,
      inboxResult: updateNotificationPageForReadFilter(
        current.inboxResult,
        notificationId,
        changedAt,
        current.inboxReadStatus,
      ),
    });

    try {
      await api.setNotificationReadState(token, notificationId, isRead);
      set((state) => ({ pendingReadIds: withoutKey(state.pendingReadIds, notificationId) }));
    } catch (error) {
      pageCache.clear();
      cacheSnapshot.forEach((value, key) => pageCache.set(key, value));
      set((state) => ({ ...stateSnapshot, pendingReadIds: withoutKey(state.pendingReadIds, notificationId) }));
      throw error;
    }
  },

  markAllRead: async (token) => {
    const snapshot = new Map(pageCache);
    const current = get();
    const now = new Date().toISOString();
    Array.from(pageCache.entries()).forEach(([key, entry]) => cachePage(key, {
      ...entry,
      data: markPageReadForFilter(entry.data, now, entry.params.readStatus ?? "all"),
      cachedAt: 0,
    }));
    set({
      previewItems: current.previewItems.map((notification) => ({ ...notification, readAt: notification.readAt ?? now })),
      unreadCount: 0,
      inboxResult: current.inboxResult
        ? markPageReadForFilter(current.inboxResult, now, current.inboxReadStatus)
        : null,
    });
    try {
      await api.markAllNotificationsRead(token);
    } catch (error) {
      pageCache.clear();
      snapshot.forEach((value, key) => pageCache.set(key, value));
      set({ previewItems: current.previewItems, unreadCount: current.unreadCount, inboxResult: current.inboxResult });
      throw error;
    }
  },

  dismissLiveToast: (toastId) => set((state) => ({ liveToasts: state.liveToasts.filter((toast) => toast.id !== toastId) })),

  reset: (userId = null) => {
    clearCache();
    resetTracking();
    set({
      userId,
      previewItems: [],
      allCount: 0,
      unreadCount: 0,
      isLoading: false,
      isPreviewRefreshing: false,
      pendingReadIds: {},
      inboxResult: null,
      inboxStatus: "idle",
      inboxPage: 1,
      inboxQuery: "",
      inboxReadStatus: "all",
      inboxCategory: "all",
      liveToasts: [],
    });
  },
}));

function findNotification(id: string, preview: NotificationItem[], page: NotificationPage | null) {
  return preview.find((item) => item.id === id) ?? page?.items.find((item) => item.id === id);
}

function updateNotificationList(items: NotificationItem[], id: string, readAt: string | null) {
  return items.map((item) => item.id === id ? { ...item, readAt } : item);
}

function updateNotificationPage(page: NotificationPage | null, id: string, readAt: string | null) {
  if (!page) {
    return null;
  }
  const target = page.items.find((item) => item.id === id);
  if (!target) {
    return page;
  }
  const wasRead = Boolean(target.readAt);
  const isRead = Boolean(readAt);
  return {
    ...page,
    items: updateNotificationList(page.items, id, readAt),
    unreadCount: wasRead === isRead ? page.unreadCount : Math.max(0, page.unreadCount + (isRead ? -1 : 1)),
  };
}

function updateCachedNotification(id: string, readAt: string | null) {
  Array.from(pageCache.entries()).forEach(([key, entry]) => {
    const updated = updateNotificationPageForReadFilter(
      entry.data,
      id,
      readAt,
      entry.params.readStatus ?? "all",
    );
    if (updated && updated !== entry.data) {
      cachePage(key, { ...entry, data: updated, cachedAt: 0 });
    }
  });
}

function updateNotificationPageForReadFilter(
  page: NotificationPage | null,
  id: string,
  readAt: string | null,
  readStatus: NotificationReadStatus,
) {
  const updated = updateNotificationPage(page, id, readAt);
  if (!updated || !page) {
    return updated;
  }
  const target = page.items.find((item) => item.id === id);
  const shouldRemove = target
    && ((readStatus === "unread" && Boolean(readAt)) || (readStatus === "read" && !readAt));
  return shouldRemove
    ? { ...updated, items: updated.items.filter((item) => item.id !== id), totalCount: Math.max(0, updated.totalCount - 1) }
    : updated;
}

function markPageRead(page: NotificationPage, readAt: string): NotificationPage {
  return {
    ...page,
    items: page.items.map((item) => ({ ...item, readAt: item.readAt ?? readAt })),
    unreadCount: 0,
  };
}

function markPageReadForFilter(
  page: NotificationPage,
  readAt: string,
  readStatus: NotificationReadStatus,
): NotificationPage {
  if (readStatus === "unread") {
    return { ...page, items: [], totalCount: 0, unreadCount: 0 };
  }
  return markPageRead(page, readAt);
}

function withoutKey(record: Record<string, true>, key: string) {
  const next = { ...record };
  delete next[key];
  return next;
}
