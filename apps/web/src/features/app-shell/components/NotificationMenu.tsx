"use client";

import { ArrowRight, Bell, LoaderCircle, Mail, MailOpen } from "lucide-react";
import type { NotificationItem } from "@/lib/types";

type NotificationMenuProps = {
  isOpen: boolean;
  items: NotificationItem[];
  label: string;
  markReadLabel: string;
  markUnreadLabel: string;
  emptyLabel: string;
  inboxLabel: string;
  isLoading: boolean;
  markAllLabel: string;
  unreadCount: number;
  pendingReadIds: Record<string, true>;
  onMarkAllRead: () => void;
  onOpenInbox: () => void;
  onSelect: (notification: NotificationItem) => void;
  onSetReadState: (notification: NotificationItem, isRead: boolean) => void;
  onToggle: () => void;
};

export function NotificationMenu({
  isOpen,
  items,
  label,
  markReadLabel,
  markUnreadLabel,
  emptyLabel,
  inboxLabel,
  isLoading,
  markAllLabel,
  unreadCount,
  pendingReadIds,
  onMarkAllRead,
  onOpenInbox,
  onSelect,
  onSetReadState,
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
            {items.slice(0, 8).map((notification) => {
              const isPending = Boolean(pendingReadIds[notification.id]);
              const isRead = Boolean(notification.readAt);
              const readLabel = isRead ? markUnreadLabel : markReadLabel;
              return (
              <article
                className={notification.readAt ? "notification-item" : "notification-item is-unread"}
                key={notification.id}
              >
                <button className="notification-item-main" onClick={() => onSelect(notification)} type="button">
                  <strong>{notification.title}</strong>
                  <span>{notification.message}</span>
                </button>
                <button
                  aria-label={readLabel}
                  className="icon-button notification-read-action"
                  disabled={isPending}
                  onClick={() => onSetReadState(notification, !isRead)}
                  title={readLabel}
                  type="button"
                >
                  {isPending ? <span aria-hidden="true" className="button-spinner" /> : isRead ? <Mail size={16} /> : <MailOpen size={16} />}
                </button>
              </article>
              );
            })}
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
