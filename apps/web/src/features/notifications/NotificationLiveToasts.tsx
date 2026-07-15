import { BellRing, X } from "lucide-react";
import { useEffect } from "react";
import type { NotificationItem } from "@/lib/types";

export function NotificationLiveToasts({
  onDismiss,
  onSelect,
  toasts,
}: {
  onDismiss: (id: string) => void;
  onSelect: (notification: NotificationItem) => void;
  toasts: Array<{ id: string; notification: NotificationItem }>;
}) {
  return (
    <div aria-live="polite" className="notification-live-toast-stack">
      {toasts.map((toast) => (
        <NotificationLiveToast key={toast.id} onDismiss={onDismiss} onSelect={onSelect} toast={toast} />
      ))}
    </div>
  );
}

function NotificationLiveToast({
  onDismiss,
  onSelect,
  toast,
}: {
  onDismiss: (id: string) => void;
  onSelect: (notification: NotificationItem) => void;
  toast: { id: string; notification: NotificationItem };
}) {
  useEffect(() => {
    const timer = window.setTimeout(() => onDismiss(toast.id), 4000);
    return () => window.clearTimeout(timer);
  }, [onDismiss, toast.id]);

  return (
    <div className="notification-live-toast">
      <button className="notification-live-toast-main" onClick={() => onSelect(toast.notification)} type="button">
        <BellRing aria-hidden="true" size={18} />
        <span><strong>{toast.notification.title}</strong><small>{toast.notification.message}</small></span>
      </button>
      <button aria-label="Kapat" className="notification-live-toast-close" onClick={() => onDismiss(toast.id)} type="button">
        <X size={15} />
      </button>
    </div>
  );
}
