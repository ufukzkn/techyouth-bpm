"use client";

import { Check, Loader2, ArrowRight } from "lucide-react";
import { statusLabel } from "@/features/i18n/translations";
import type { Language, ProcessStatus } from "@/lib/types";

type ProcessFlowIndicatorProps = {
  status: ProcessStatus;
  language: Language;
};

export function ProcessFlowIndicator({ status, language }: ProcessFlowIndicatorProps) {
  // Define the main flow steps
  const isPending = status === "Pending";
  const isInProgress = status === "InProgress";
  const isTerminal = status === "Completed" || status === "Rejected" || status === "Escalated";

  return (
    <div className="process-flow-indicator" aria-label="Process flow indicator">
      {/* Step 1: Pending */}
      <div className={`flow-step ${isPending ? "active" : "completed"}`}>
        <div className="flow-step-icon">
          {isPending ? <Loader2 className="spin-icon" size={16} /> : <Check size={16} />}
        </div>
        <span className="flow-step-label">{statusLabel(language, "Pending")}</span>
      </div>

      <div className={`flow-connector ${!isPending ? "active" : ""}`} />

      {/* Step 2: InProgress */}
      <div className={`flow-step ${isInProgress ? "active" : isTerminal ? "completed" : "pending"}`}>
        <div className="flow-step-icon">
          {isInProgress ? <Loader2 className="spin-icon" size={16} /> : isTerminal ? <Check size={16} /> : <div className="dot" />}
        </div>
        <span className="flow-step-label">{statusLabel(language, "InProgress")}</span>
      </div>

      <div className={`flow-connector ${isTerminal ? "active" : ""}`} />

      {/* Step 3: Terminal State */}
      <div className={`flow-step ${isTerminal ? `active terminal-${status.toLowerCase()}` : "pending"}`}>
        <div className="flow-step-icon">
          {isTerminal ? <ArrowRight size={16} /> : <div className="dot" />}
        </div>
        <span className="flow-step-label">
          {isTerminal ? statusLabel(language, status) : statusLabel(language, "Completed")}
        </span>
      </div>
    </div>
  );
}
