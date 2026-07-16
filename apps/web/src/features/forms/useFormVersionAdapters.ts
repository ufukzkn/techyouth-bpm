"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  fromFormDefinitionVersion,
  toCreateFormVersionRequest,
  type FormVersionAdapter,
  type FormVersionPersistenceInput,
  type VersionedFormLayout,
} from "@/features/forms/formVersioning";
import { useSessionStore } from "@/features/session/sessionStore";
import { api } from "@/lib/api";
import type { CreateFormPageRequest, FormDefinitionVersion } from "@/lib/types";

let cacheToken: string | null = null;
let versionCache = new Map<string, FormDefinitionVersion[]>();

export function useFormVersionAdapters() {
  const token = useSessionStore((state) => state.token);
  const [versionsByForm, setVersionsByForm] = useState(() => new Map(versionCache));
  const [isReady, setIsReady] = useState(() => !token || token.startsWith("demo-") || cacheToken === token);

  useEffect(() => {
    let cancelled = false;
    if (!token || token.startsWith("demo-")) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setIsReady(true);
      return;
    }

    if (cacheToken === token) {
      setVersionsByForm(new Map(versionCache));
      setIsReady(true);
      return;
    }

    setIsReady(false);
    void api.listForms(token)
      .then(async (forms) => {
        const entries = await Promise.all(forms.map(async (form) => [form.id, await api.listFormVersions(token, form.id)] as const));
        if (cancelled) return;
        cacheToken = token;
        versionCache = new Map(entries);
        setVersionsByForm(new Map(versionCache));
      })
      .catch(() => {
        if (!cancelled) {
          cacheToken = token;
          versionCache = new Map();
          setVersionsByForm(new Map());
        }
      })
      .finally(() => {
        if (!cancelled) setIsReady(true);
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  const storeVersion = useCallback((version: FormDefinitionVersion) => {
    const current = versionCache.get(version.formDefinitionId) ?? [];
    const next = [version, ...current.filter((candidate) => candidate.id !== version.id)]
      .sort((left, right) => right.versionNumber - left.versionNumber);
    versionCache.set(version.formDefinitionId, next);
    setVersionsByForm(new Map(versionCache));
  }, []);

  const resolveDesignerLayout = useCallback((formId: string) => {
    const versions = versionsByForm.get(formId) ?? [];
    return versions.find((version) => version.status === "Draft")
      ?? versions.find((version) => version.status === "Published")
      ?? versions[0];
  }, [versionsByForm]);

  const resolveRunnerLayout = useCallback((formId: string) => {
    const versions = versionsByForm.get(formId) ?? [];
    return versions.find((version) => version.status === "Published") ?? versions[0];
  }, [versionsByForm]);

  const persistDraft = useCallback(async (input: FormVersionPersistenceInput) => {
    if (!token || token.startsWith("demo-")) return { ...input.layout, status: "draft" as const };
    const versions = versionCache.get(input.form.id) ?? [];
    const currentDraft = versions.find((version) => version.status === "Draft");
    const payload = toCreateFormVersionRequest(input);
    const persisted = currentDraft
      ? await api.updateFormVersion(token, input.form.id, currentDraft.id, payload)
      : await api.createFormVersion(token, input.form.id, payload);
    storeVersion(persisted);
    return formVersionToLayout(persisted);
  }, [storeVersion, token]);

  const publish = useCallback(async (input: FormVersionPersistenceInput) => {
    if (!token || token.startsWith("demo-")) return { ...input.layout, status: "published" as const };
    const versions = versionCache.get(input.form.id) ?? [];
    const currentPublished = versions.find((version) => version.status === "Published");
    const currentDraft = versions.find((version) => version.status === "Draft");

    if (!currentDraft && currentPublished?.versionNumber === input.layout.version) {
      return formVersionToLayout(currentPublished);
    }

    const payload = toCreateFormVersionRequest(input);
    const draft = currentDraft
      ? await api.updateFormVersion(token, input.form.id, currentDraft.id, payload)
      : await api.createFormVersion(token, input.form.id, payload);
    storeVersion(draft);
    const published = await api.publishFormVersion(token, input.form.id, draft.id);
    storeVersion(published);
    return formVersionToLayout(published);
  }, [storeVersion, token]);

  const archive = useCallback(async (input: FormVersionPersistenceInput) => {
    if (!input.layout.versionId) {
      throw new Error("A persisted form version is required before archiving.");
    }
    if (!token || token.startsWith("demo-")) return { ...input.layout, status: "archived" as const };
    const archived = await api.archiveFormVersion(token, input.form.id, input.layout.versionId);
    storeVersion(archived);
    return formVersionToLayout(archived);
  }, [storeVersion, token]);

  const designerAdapter = useMemo<FormVersionAdapter>(() => ({
    resolveVersion: (form) => {
      const version = resolveDesignerLayout(form.id);
      return version ? fromFormDefinitionVersion(version) : undefined;
    },
    resolveLayout: (form) => {
      const version = resolveDesignerLayout(form.id);
      return version ? formVersionToLayout(version) : undefined;
    },
    saveDraft: persistDraft,
    publish,
    archive,
  }), [archive, persistDraft, publish, resolveDesignerLayout]);

  const runnerAdapter = useMemo<Pick<FormVersionAdapter, "resolveLayout" | "resolveVersion">>(() => ({
    resolveVersion: (form) => {
      const version = resolveRunnerLayout(form.id);
      return version ? fromFormDefinitionVersion(version) : undefined;
    },
    resolveLayout: (form) => {
      const version = resolveRunnerLayout(form.id);
      return version ? formVersionToLayout(version) : undefined;
    },
  }), [resolveRunnerLayout]);

  return { designerAdapter, isReady, runnerAdapter };
}

export function formVersionToLayout(version: FormDefinitionVersion): VersionedFormLayout {
  return fromFormDefinitionVersion(version).layout;
}

export function formPersistenceToPages(input: FormVersionPersistenceInput): CreateFormPageRequest[] {
  return toCreateFormVersionRequest(input).pages;
}
