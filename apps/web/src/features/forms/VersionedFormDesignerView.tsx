"use client";

import { SkeletonBlock } from "@/features/app-shell/components/AsyncState";
import { FormDesignerDraft } from "@/features/form-designer/FormDesignerDraft";
import { useFormVersionAdapters } from "@/features/forms/useFormVersionAdapters";
import { translate } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";

export function VersionedFormDesignerView() {
  const language = useSessionStore((state) => state.language);
  const { designerAdapter, isReady } = useFormVersionAdapters();
  return isReady
    ? <FormDesignerDraft versionAdapter={designerAdapter} />
    : <FormVersionLoading label={translate(language, "form.designer.loadingForms")} />;
}

function FormVersionLoading({ label }: { label: string }) {
  return (
    <section className="designer-section form-version-loading" role="status" aria-label={label}>
      <SkeletonBlock />
      <SkeletonBlock />
      <SkeletonBlock />
    </section>
  );
}
