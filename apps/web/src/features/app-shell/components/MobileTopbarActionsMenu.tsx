"use client";

import { Ellipsis, Languages, ListTodo, LogOut, SunMoon } from "lucide-react";
import { useEffect, useRef, type KeyboardEvent as ReactKeyboardEvent } from "react";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language, ThemeMode } from "@/lib/types";

type MobileTopbarActionsMenuProps = {
  canAccessTasks: boolean;
  isOpen: boolean;
  language: Language;
  theme: ThemeMode;
  onClose: () => void;
  onLogout: () => void;
  onOpenTasks: () => void;
  onToggle: () => void;
  onToggleLanguage: () => void;
  onToggleTheme: () => void;
};

export function MobileTopbarActionsMenu({
  canAccessTasks,
  isOpen,
  language,
  theme,
  onClose,
  onLogout,
  onOpenTasks,
  onToggle,
  onToggleLanguage,
  onToggleTheme,
}: MobileTopbarActionsMenuProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const t = (key: TranslationKey) => translate(language, key);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function closeOnOutsidePointer(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        onClose();
      }
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
        triggerRef.current?.focus();
      }
    }

    document.addEventListener("pointerdown", closeOnOutsidePointer);
    document.addEventListener("keydown", closeOnEscape);
    const focusFrame = window.requestAnimationFrame(() => {
      menuRef.current?.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus();
    });
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("pointerdown", closeOnOutsidePointer);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isOpen, onClose]);

  function runAction(action: () => void) {
    onClose();
    action();
  }

  function moveMenuFocus(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
      return;
    }

    const items = Array.from(
      menuRef.current?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]') ?? [],
    );
    if (!items.length) {
      return;
    }

    event.preventDefault();
    const currentIndex = items.indexOf(document.activeElement as HTMLButtonElement);
    if (event.key === "Home") {
      items[0].focus();
    } else if (event.key === "End") {
      items.at(-1)?.focus();
    } else {
      const offset = event.key === "ArrowDown" ? 1 : -1;
      const nextIndex = (Math.max(currentIndex, 0) + offset + items.length) % items.length;
      items[nextIndex].focus();
    }
  }

  return (
    <div className="mobile-actions-menu" ref={containerRef}>
      <button
        aria-controls="mobile-topbar-actions"
        aria-expanded={isOpen}
        aria-haspopup="menu"
        aria-label={t("common.moreActions")}
        className="icon-button mobile-actions-trigger"
        onClick={onToggle}
        ref={triggerRef}
        title={t("common.moreActions")}
        type="button"
      >
        <Ellipsis size={19} />
      </button>
      {isOpen ? (
        <div
          aria-label={t("common.moreActions")}
          className="mobile-actions-popover"
          id="mobile-topbar-actions"
          onKeyDown={moveMenuFocus}
          ref={menuRef}
          role="menu"
        >
          {canAccessTasks ? (
            <button onClick={() => runAction(onOpenTasks)} role="menuitem" type="button">
              <ListTodo size={17} />
              <span>{t("common.openMyTasks")}</span>
            </button>
          ) : null}
          <button onClick={() => runAction(onToggleLanguage)} role="menuitem" type="button">
            <Languages size={17} />
            <span>{t("common.language")}</span>
            <strong>{language.toUpperCase()}</strong>
          </button>
          <button onClick={() => runAction(onToggleTheme)} role="menuitem" type="button">
            <SunMoon size={17} />
            <span>{t("common.theme")}</span>
            <strong>{t(theme === "dark" ? "common.themeDark" : "common.themeLight")}</strong>
          </button>
          <button className="mobile-actions-logout" onClick={() => runAction(onLogout)} role="menuitem" type="button">
            <LogOut size={17} />
            <span>{t("common.logout")}</span>
          </button>
        </div>
      ) : null}
    </div>
  );
}
