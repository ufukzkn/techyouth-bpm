"use client";

import { Bell } from "lucide-react";
import type { NotificationItem } from "@/lib/types";

type NotificationMenuProps = {
  isOpen: boolean;
  items: NotificationItem[];
  label: string;
  emptyLabel: string;
  markAllLabel: string;
  onMarkAllRead: () => void;
  onMarkRead: (notificationId: string) => void;
  onToggle: () => void;
};

export function NotificationMenu({
  isOpen,
  items,
  label,
  emptyLabel,
  markAllLabel,
  onMarkAllRead,
  onMarkRead,
  onToggle,
}: NotificationMenuProps) {
  const unreadCount = items.filter((notification) => !notification.readAt).length;

  return (
    <div className="notification-menu">
      <button
        aria-expanded={isOpen}
        aria-label={label}
        className="icon-button notification-button"
        onClick={onToggle}
        title={label}
        type="button"
      >
        <Bell size={18} />
        {unreadCount > 0 ? <span className="notification-badge">{unreadCount}</span> : null}
      </button>
      {isOpen ? (
        <div className="notification-popover" role="dialog" aria-label={label}>
          <div className="notification-popover-header">
            <strong>{label}</strong>
            <button className="text-button" type="button" onClick={onMarkAllRead}>
              {markAllLabel}
            </button>
          </div>
          <div className="notification-list">
            {items.slice(0, 8).map((notification) => (
              <button
                className={notification.readAt ? "notification-item" : "notification-item is-unread"}
                key={notification.id}
                onClick={() => onMarkRead(notification.id)}
                type="button"
              >
                <strong>{notification.title}</strong>
                <span>{notification.message}</span>
              </button>
            ))}
            {!items.length ? <p className="status-line">{emptyLabel}</p> : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
