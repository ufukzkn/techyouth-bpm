import { LoaderCircle } from "lucide-react";

export function InlineValueLoader({ label = "Yükleniyor" }: { label?: string }) {
  return (
    <span className="inline-value-loader" aria-label={label} role="status">
      <LoaderCircle className="spin-icon" size={18} aria-hidden="true" />
    </span>
  );
}

export function SkeletonBlock({ className = "" }: { className?: string }) {
  return <span className={`skeleton-block ${className}`.trim()} aria-hidden="true" />;
}

export function ActionFeedback({
  feedback,
}: {
  feedback: { tone: "success" | "error" | "loading"; text: string } | null;
}) {
  if (!feedback) {
    return null;
  }

  return (
    <p className={`action-feedback action-feedback-${feedback.tone}`} role="status" aria-live="polite">
      {feedback.tone === "loading" ? <LoaderCircle className="spin-icon" size={15} aria-hidden="true" /> : null}
      {feedback.text}
    </p>
  );
}
