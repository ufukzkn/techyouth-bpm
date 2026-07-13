"use client";

import { create } from "zustand";
import { api } from "@/lib/api";
import type { NotificationItem } from "@/lib/types";

type NotificationState = {
  userId: string | null;
  previewItems: NotificationItem[];
  allCount: number;
  unreadCount: number;
  isLoading: boolean;
  loadPreview: (token: string, userId: string) => Promise<void>;
  setReadState: (token: string, notificationId: string, isRead: boolean) => Promise<void>;
  markAllRead: (token: string) => Promise<void>;
  reset: (userId?: string | null) => void;
};

export const useNotificationStore = create<NotificationState>((set, get) => ({
  userId: null,
  previewItems: [],
  allCount: 0,
  unreadCount: 0,
  isLoading: false,
  loadPreview: async (token, userId) => {
    if (get().userId !== userId) {
      set({ userId, previewItems: [], allCount: 0, unreadCount: 0 });
    }
    set({ isLoading: true });
    try {
      const page = await api.listNotifications(token, { page: 1, pageSize: 5 });
      if (get().userId === userId) {
        set({
          previewItems: page.items,
          allCount: page.allCount,
          unreadCount: page.unreadCount,
          isLoading: false,
        });
      }
    } catch (error) {
      set({ isLoading: false });
      throw error;
    }
  },
  setReadState: async (token, notificationId, isRead) => {
    await api.setNotificationReadState(token, notificationId, isRead);
    const current = get();
    const item = current.previewItems.find((notification) => notification.id === notificationId);
    const wasRead = Boolean(item?.readAt);
    set({
      previewItems: current.previewItems.map((notification) =>
        notification.id === notificationId
          ? { ...notification, readAt: isRead ? notification.readAt ?? new Date().toISOString() : null }
          : notification,
      ),
      unreadCount: wasRead === isRead
        ? current.unreadCount
        : Math.max(0, current.unreadCount + (isRead ? -1 : 1)),
    });
  },
  markAllRead: async (token) => {
    await api.markAllNotificationsRead(token);
    const now = new Date().toISOString();
    set((current) => ({
      previewItems: current.previewItems.map((notification) => ({ ...notification, readAt: notification.readAt ?? now })),
      unreadCount: 0,
    }));
  },
  reset: (userId = null) => set({ userId, previewItems: [], allCount: 0, unreadCount: 0, isLoading: false }),
}));
