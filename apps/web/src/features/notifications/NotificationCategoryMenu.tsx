import { Bell, Check, ChevronDown, CircleUserRound, ClipboardCheck, UserCog, Workflow } from "lucide-react";
import { useEffect, useRef, useState, type ComponentType } from "react";
import type { NotificationCategory } from "@/lib/types";

type CategoryOption = {
  value: NotificationCategory;
  label: string;
  icon: ComponentType<{ size?: number; "aria-hidden"?: boolean }>;
};

export function NotificationCategoryMenu({
  label,
  onChange,
  options,
  value,
}: {
  label: string;
  onChange: (value: NotificationCategory) => void;
  options: Array<{ value: NotificationCategory; label: string }>;
  value: NotificationCategory;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const iconByCategory: Record<NotificationCategory, CategoryOption["icon"]> = {
    all: Bell,
    task: ClipboardCheck,
    process: Workflow,
    access: UserCog,
    account: CircleUserRound,
  };
  const selected = options.find((option) => option.value === value) ?? options[0];
  const SelectedIcon = iconByCategory[selected.value];

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function closeOnOutsideClick(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
        return;
      }
      if (event.key !== "ArrowDown" && event.key !== "ArrowUp") {
        return;
      }
      event.preventDefault();
      const activeIndex = buttonRefs.current.findIndex((button) => button === document.activeElement);
      const direction = event.key === "ArrowDown" ? 1 : -1;
      const nextIndex = activeIndex < 0 ? 0 : (activeIndex + direction + options.length) % options.length;
      buttonRefs.current[nextIndex]?.focus();
    }

    document.addEventListener("pointerdown", closeOnOutsideClick);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutsideClick);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, options.length]);

  return (
    <div className="notification-category-menu" ref={rootRef}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="menu"
        className="notification-category-trigger"
        onClick={() => setIsOpen((open) => !open)}
        type="button"
      >
        <SelectedIcon aria-hidden size={16} />
        <span>{selected.label}</span>
        <ChevronDown aria-hidden="true" className={isOpen ? "open" : undefined} size={15} />
      </button>
      {isOpen ? (
        <div aria-label={label} className="notification-category-popover" role="menu">
          {options.map((option, index) => {
            const Icon = iconByCategory[option.value];
            return (
              <button
                aria-checked={option.value === value}
                key={option.value}
                onClick={() => {
                  onChange(option.value);
                  setIsOpen(false);
                }}
                ref={(element) => { buttonRefs.current[index] = element; }}
                role="menuitemradio"
                type="button"
              >
                <Icon aria-hidden size={16} />
                <span>{option.label}</span>
                {option.value === value ? <Check aria-hidden="true" size={15} /> : null}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
