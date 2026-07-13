"use client";

import { ArrowRight, Bell, LoaderCircle } from "lucide-react";
import type { NotificationItem } from "@/lib/types";

type NotificationMenuProps = {
  isOpen: boolean;
  items: NotificationItem[];
  label: string;
  emptyLabel: string;
  inboxLabel: string;
  isLoading: boolean;
  markAllLabel: string;
  unreadCount: number;
  onMarkAllRead: () => void;
  onOpenInbox: () => void;
  onSelect: (notification: NotificationItem) => void;
  onToggle: () => void;
};

export function NotificationMenu({
  isOpen,
  items,
  label,
  emptyLabel,
  inboxLabel,
  isLoading,
  markAllLabel,
  unreadCount,
  onMarkAllRead,
  onOpenInbox,
  onSelect,
  onToggle,
}: NotificationMenuProps) {
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
        {unreadCount > 0 ? <span className="notification-badge">{unreadCount > 99 ? "99+" : unreadCount}</span> : null}
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
                onClick={() => onSelect(notification)}
                type="button"
              >
                <strong>{notification.title}</strong>
                <span>{notification.message}</span>
              </button>
            ))}
            {isLoading && !items.length ? <p className="notification-loading"><LoaderCircle className="spin-icon" size={18} /> {label}</p> : null}
            {!isLoading && !items.length ? <p className="status-line">{emptyLabel}</p> : null}
          </div>
          <button className="notification-inbox-link" type="button" onClick={onOpenInbox}>
            {inboxLabel}
            <ArrowRight size={15} aria-hidden="true" />
          </button>
        </div>
      ) : null}
    </div>
  );
}
