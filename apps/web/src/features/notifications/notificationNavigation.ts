import type { NotificationItem } from "@/lib/types";

export function getNotificationTarget(notification: NotificationItem): string | null {
  if (notification.type.startsWith("Task.")) {
    return `/tasks${notification.entityId ? `?processId=${encodeURIComponent(notification.entityId)}` : ""}`;
  }

  if (notification.type.startsWith("Process.")) {
    return `/processes${notification.entityId ? `?processId=${encodeURIComponent(notification.entityId)}` : ""}`;
  }

  if (notification.type === "User.PendingApproval") {
    return "/management/users?status=PendingApproval";
  }

  if (notification.type.startsWith("User.") || notification.type.startsWith("Community.")) {
    return "/settings";
  }

  return null;
}
