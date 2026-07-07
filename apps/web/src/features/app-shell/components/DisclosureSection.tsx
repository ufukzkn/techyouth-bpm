import { ChevronDown } from "lucide-react";
import type { ReactNode } from "react";

export function DisclosureSection({
  children,
  className,
  description,
  eyebrow,
  icon,
  isOpen,
  onToggle,
  title,
}: {
  children: ReactNode;
  className?: string;
  description: string;
  eyebrow: string;
  icon: ReactNode;
  isOpen: boolean;
  onToggle: () => void;
  title: string;
}) {
  return (
    <section className={["identity-section disclosure-section", className].filter(Boolean).join(" ")}>
      <button className="disclosure-trigger" type="button" aria-expanded={isOpen} onClick={onToggle}>
        <div className="disclosure-title-group">
          <span className="disclosure-leading-icon">{icon}</span>
          <div>
            <span className="eyebrow">{eyebrow}</span>
            <h3>{title}</h3>
            <p className="helper-copy">{description}</p>
          </div>
        </div>
        <span className="disclosure-icons" aria-hidden="true">
          <ChevronDown className={isOpen ? "disclosure-chevron open" : "disclosure-chevron"} size={18} />
        </span>
      </button>
      {isOpen ? <div className="disclosure-content">{children}</div> : null}
    </section>
  );
}
