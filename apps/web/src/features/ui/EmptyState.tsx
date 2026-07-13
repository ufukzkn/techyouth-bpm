import type { ReactNode } from "react";

type EmptyStateProps = {
  action?: ReactNode;
  description: string;
  icon?: ReactNode;
  title: string;
};

export function EmptyState({ action, description, icon, title }: EmptyStateProps) {
  return (
    <div className="ui-empty-state" role="status">
      {icon ? <span className="ui-empty-state-icon" aria-hidden="true">{icon}</span> : null}
      <div>
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
      {action ? <div className="ui-empty-state-action">{action}</div> : null}
    </div>
  );
}
