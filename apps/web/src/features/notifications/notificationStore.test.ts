import { beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "@/lib/api";
import type { NotificationPage } from "@/lib/types";
import { useNotificationStore } from "@/features/notifications/notificationStore";

vi.mock("@/lib/api", () => ({
  api: {
    listNotifications: vi.fn(),
    setNotificationReadState: vi.fn(),
    markAllNotificationsRead: vi.fn(),
  },
}));

const unreadPage: NotificationPage = {
  items: [{
    id: "notification-1",
    type: "Team.MembershipAdded",
    title: "Takım üyeliği",
    message: "Scout Ekibi takımına eklendiniz.",
    entityType: "Team",
    entityId: "team-1",
    createdAt: "2026-07-14T12:00:00Z",
    readAt: null,
  }],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  allCount: 1,
  unreadCount: 1,
};

describe("notificationStore", () => {
  beforeEach(() => {
    useNotificationStore.getState().reset();
    vi.clearAllMocks();
  });

  it("reuses a fresh inbox page without a second API request", async () => {
    vi.mocked(api.listNotifications).mockResolvedValue(unreadPage);

    await useNotificationStore.getState().loadInbox("token", "user-1");
    await useNotificationStore.getState().loadInbox("token", "user-1");

    expect(api.listNotifications).toHaveBeenCalledTimes(1);
    expect(useNotificationStore.getState().inboxResult?.items).toHaveLength(1);
    expect(useNotificationStore.getState().inboxStatus).toBe("idle");
  });

  it("keeps the visible page while an uncached filter is loading", async () => {
    vi.mocked(api.listNotifications).mockResolvedValueOnce(unreadPage);
    await useNotificationStore.getState().loadInbox("token", "user-1");

    useNotificationStore.getState().setInboxFilters({ readStatus: "unread" });
    let resolveRequest: ((page: NotificationPage) => void) | undefined;
    vi.mocked(api.listNotifications).mockImplementationOnce(
      () => new Promise<NotificationPage>((resolve) => { resolveRequest = resolve; }),
    );
    const reload = useNotificationStore.getState().loadInbox("token", "user-1");

    const pendingFilter = useNotificationStore.getState();
    expect(pendingFilter.inboxStatus).toBe("refreshing");
    expect(pendingFilter.inboxResult?.items).toEqual(unreadPage.items);

    resolveRequest?.({ ...unreadPage, items: [] });
    await reload;
    expect(useNotificationStore.getState().inboxResult?.items).toEqual([]);
  });

  it("updates read state optimistically and clears the pending marker after success", async () => {
    vi.mocked(api.listNotifications).mockResolvedValue(unreadPage);
    let resolveRequest: (() => void) | undefined;
    vi.mocked(api.setNotificationReadState).mockImplementation(
      () => new Promise<void>((resolve) => { resolveRequest = resolve; }),
    );
    await useNotificationStore.getState().loadInbox("token", "user-1");

    const request = useNotificationStore.getState().setReadState("token", "notification-1", true);
    const optimistic = useNotificationStore.getState();

    expect(optimistic.inboxResult?.items[0].readAt).not.toBeNull();
    expect(optimistic.unreadCount).toBe(0);
    expect(optimistic.pendingReadIds["notification-1"]).toBe(true);

    resolveRequest?.();
    await request;
    expect(useNotificationStore.getState().pendingReadIds["notification-1"]).toBeUndefined();
  });

  it("rolls optimistic read state back when the API rejects the mutation", async () => {
    vi.mocked(api.listNotifications).mockResolvedValue(unreadPage);
    vi.mocked(api.setNotificationReadState).mockRejectedValue(new Error("network"));
    await useNotificationStore.getState().loadInbox("token", "user-1");

    await expect(
      useNotificationStore.getState().setReadState("token", "notification-1", true),
    ).rejects.toThrow("network");

    const rolledBack = useNotificationStore.getState();
    expect(rolledBack.inboxResult?.items[0].readAt).toBeNull();
    expect(rolledBack.unreadCount).toBe(1);
    expect(rolledBack.pendingReadIds["notification-1"]).toBeUndefined();
  });

  it("clears cached pages when the active user changes", async () => {
    vi.mocked(api.listNotifications).mockResolvedValue(unreadPage);

    await useNotificationStore.getState().loadInbox("token", "user-1");
    await useNotificationStore.getState().loadInbox("token", "user-2");

    expect(api.listNotifications).toHaveBeenCalledTimes(2);
    expect(useNotificationStore.getState().userId).toBe("user-2");
  });
});
