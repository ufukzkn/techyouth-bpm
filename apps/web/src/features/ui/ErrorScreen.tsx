"use client";

import { AlertTriangle, LayoutDashboard, LogIn, RefreshCw } from "lucide-react";
import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

type ErrorScreenProps = {
  language: Language;
  reference?: string;
  onRetry: () => void;
  onDashboard: () => void;
  onLogin: () => void;
};

export function ErrorScreen({
  language,
  reference,
  onRetry,
  onDashboard,
  onLogin,
}: ErrorScreenProps) {
  return (
    <main className="error-screen">
      <section className="error-screen-panel" role="alert">
        <span className="error-screen-icon" aria-hidden="true">
          <AlertTriangle size={28} />
        </span>
        <div className="error-screen-copy">
          <p className="eyebrow">{translate(language, "error.eyebrow")}</p>
          <h1>{translate(language, "error.title")}</h1>
          <p>{translate(language, "error.description")}</p>
          {reference ? (
            <small>{translate(language, "error.reference", { reference })}</small>
          ) : null}
        </div>
        <div className="error-screen-actions">
          <button className="primary-button" type="button" onClick={onRetry}>
            <RefreshCw size={17} aria-hidden="true" />
            {translate(language, "error.retry")}
          </button>
          <button className="secondary-button" type="button" onClick={onDashboard}>
            <LayoutDashboard size={17} aria-hidden="true" />
            {translate(language, "error.dashboard")}
          </button>
          <button className="secondary-button" type="button" onClick={onLogin}>
            <LogIn size={17} aria-hidden="true" />
            {translate(language, "error.login")}
          </button>
        </div>
      </section>
    </main>
  );
}
