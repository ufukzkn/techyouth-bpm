"use client";

import { useSyncExternalStore } from "react";
import type { ThemeMode } from "@/lib/types";

const subscribeToClientReady = () => () => {};

type ThemeToggleButtonProps = {
  theme: ThemeMode;
  label: string;
  onToggle: () => void;
};

export function ThemeToggleButton({ theme, label, onToggle }: ThemeToggleButtonProps) {
  const isClientReady = useSyncExternalStore(subscribeToClientReady, () => true, () => false);

  return (
    <button
      className="icon-button theme-toggle-button"
      data-theme-mode={isClientReady ? theme : "light"}
      onClick={onToggle}
      aria-label={label}
      title={label}
      type="button"
    >
      <span className="theme-toggle-orb" aria-hidden="true" />
    </button>
  );
}
