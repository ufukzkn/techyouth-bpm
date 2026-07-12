"use client";

import type { Language } from "@/lib/types";

type LanguageToggleButtonProps = {
  language: Language;
  label: string;
  onToggle: () => void;
};

export function LanguageToggleButton({ language, label, onToggle }: LanguageToggleButtonProps) {
  return (
    <button
      className="icon-button language-toggle-button"
      data-language={language}
      onClick={onToggle}
      aria-label={label}
      title={label}
      type="button"
    >
      <span className="language-toggle-orbit" aria-hidden="true" />
      <span className="language-toggle-track" aria-hidden="true">
        <span className="language-toggle-code language-toggle-code-tr">TR</span>
        <span className="language-toggle-code language-toggle-code-en">EN</span>
      </span>
    </button>
  );
}
