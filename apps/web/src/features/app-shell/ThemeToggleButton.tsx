"use client";

import type { ThemeMode } from "@/lib/types";

type ThemeToggleButtonProps = {
  theme: ThemeMode;
  label: string;
  onToggle: () => void;
};

export function ThemeToggleButton({ theme, label, onToggle }: ThemeToggleButtonProps) {
  return (
    <button
      className="icon-button theme-toggle-button"
      data-theme-mode={theme}
      onClick={onToggle}
      aria-label={label}
      title={label}
      type="button"
    >
      <span className="theme-toggle-orb" aria-hidden="true" />
    </button>
  );
}
