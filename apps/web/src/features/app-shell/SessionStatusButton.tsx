"use client";

type SessionStatusButtonProps = {
  expanded: boolean;
  label: string;
  onToggle: () => void;
};

export function SessionStatusButton({ expanded, label, onToggle }: SessionStatusButtonProps) {
  return (
    <button
      className="session-icon-button session-status-button"
      type="button"
      aria-expanded={expanded}
      aria-label={label}
      title={label}
      onClick={onToggle}
    >
      <svg className="session-status-shield" viewBox="0 0 24 24" aria-hidden="true">
        <path
          className="session-status-shield-path"
          d="M12 3.2 18.4 5.6v5.7c0 4.3-2.6 7.6-6.4 9.1-3.8-1.5-6.4-4.8-6.4-9.1V5.6L12 3.2Z"
        />
        <path className="session-status-check-path" d="m8.6 12.2 2.2 2.2 4.8-5" />
      </svg>
    </button>
  );
}
