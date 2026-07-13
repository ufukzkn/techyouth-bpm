"use client";

import { Check, Copy, Maximize2, Minimize2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { IconButton } from "@/features/ui/IconButton";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

type JsonViewerProps = {
  className?: string;
  language: Language;
  value: unknown;
};

export function JsonViewer({ className = "", language, value }: JsonViewerProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [copyState, setCopyState] = useState<"idle" | "copied" | "error">("idle");
  const json = useMemo(() => JSON.stringify(value, null, 2) ?? "null", [value]);

  useEffect(() => {
    if (copyState === "idle") {
      return;
    }

    const timeout = window.setTimeout(() => setCopyState("idle"), 1800);
    return () => window.clearTimeout(timeout);
  }, [copyState]);

  async function copyJson() {
    try {
      await navigator.clipboard.writeText(json);
      setCopyState("copied");
    } catch {
      setCopyState("error");
    }
  }

  return (
    <div className={`json-viewer${isExpanded ? " json-viewer-expanded" : ""}`}>
      <div className="json-viewer-toolbar">
        <span className={`json-copy-feedback json-copy-feedback-${copyState}`} aria-live="polite">
          {copyState === "copied"
            ? translate(language, "common.copied")
            : copyState === "error"
              ? translate(language, "common.copyFailed")
              : ""}
        </span>
        <IconButton label={translate(language, "common.copy")} onClick={copyJson}>
          {copyState === "copied" ? <Check size={16} /> : <Copy size={16} />}
        </IconButton>
        <IconButton
          label={translate(language, isExpanded ? "common.collapse" : "common.expand")}
          onClick={() => setIsExpanded((current) => !current)}
        >
          {isExpanded ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
        </IconButton>
      </div>
      <pre className={`json-preview ${className}`.trim()}>{json}</pre>
    </div>
  );
}
