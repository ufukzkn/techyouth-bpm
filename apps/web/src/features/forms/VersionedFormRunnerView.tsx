"use client";

import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { FormRunnerDraft } from "@/features/form-runner/FormRunnerDraft";
import { useFormVersionAdapters } from "@/features/forms/useFormVersionAdapters";
import { translate } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";

export function VersionedFormRunnerView() {
  const language = useSessionStore((state) => state.language);
  const { isReady, runnerAdapter } = useFormVersionAdapters();
  return isReady
    ? <FormRunnerDraft versionAdapter={runnerAdapter} />
    : <FormVersionLoading label={translate(language, "form.runner.loadingForms")} />;
}

function FormVersionLoading({ label }: { label: string }) {
  return (
    <section className="process-section form-version-loading" role="status" aria-label={label}>
      <SkeletonBlock />
      <SkeletonBlock />
      <SkeletonBlock />
    </section>
  );
}
