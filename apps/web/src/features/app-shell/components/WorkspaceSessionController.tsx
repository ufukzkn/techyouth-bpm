"use client";

import { useCallback, useEffect } from "react";
import { useRouter } from "next/navigation";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError, setUnauthorizedHandler } from "@/lib/api";

const maxBrowserTimeoutDelay = 2_147_483_647;

export function WorkspaceSessionController() {
  const router = useRouter();
  const {
    user,
    token,
    expiresAt,
    theme,
    language,
    hasHydrated,
    expireSession,
    setSession,
    syncSystemTheme,
  } = useSessionStore();
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  useEffect(() => {
    document.documentElement.lang = language;
    document.title = "TechYouth BPM Wizard";
  }, [language]);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    const syncTheme = () => syncSystemTheme(mediaQuery.matches ? "dark" : "light");

    syncTheme();
    mediaQuery.addEventListener("change", syncTheme);
    return () => mediaQuery.removeEventListener("change", syncTheme);
  }, [syncSystemTheme]);

  useEffect(() => {
    let hasHandledUnauthorized = false;
    setUnauthorizedHandler(() => {
      if (hasHandledUnauthorized) {
        return;
      }

      hasHandledUnauthorized = true;
      expireSession(t("session.unverified"));
      router.replace("/login");
    });

    return () => setUnauthorizedHandler(null);
  }, [expireSession, router, t]);

  useEffect(() => {
    if (!hasHydrated || !token || !user) {
      return;
    }

    let ignore = false;
    let expiryTimer: number | undefined;
    const sessionToken = token;
    const expiresAtTime = expiresAt ? Date.parse(expiresAt) : null;

    async function refreshOrExpire(message: string) {
      if (sessionToken.startsWith("demo-")) {
        expireSession(message);
        return;
      }

      try {
        const refreshedSession = await api.refreshSession();
        if (!ignore) {
          setSession(refreshedSession);
        }
      } catch {
        if (!ignore) {
          expireSession(message);
        }
      }
    }

    if (expiresAtTime && expiresAtTime <= Date.now()) {
      void refreshOrExpire(t("session.expired"));
      return;
    }

    function scheduleExpiryCheck() {
      if (!expiresAtTime) {
        return;
      }

      const remainingMs = expiresAtTime - Date.now();
      if (remainingMs <= 0) {
        void refreshOrExpire(t("session.expired"));
        return;
      }

      expiryTimer = window.setTimeout(scheduleExpiryCheck, Math.min(remainingMs, maxBrowserTimeoutDelay));
    }

    scheduleExpiryCheck();

    async function verifySession() {
      if (sessionToken.startsWith("demo-")) {
        return;
      }

      try {
        await api.me(sessionToken);
      } catch (error) {
        if (!ignore && error instanceof ApiError && error.statusCode === 401) {
          await refreshOrExpire(t("session.unverified"));
        }
      }
    }

    void verifySession();

    return () => {
      ignore = true;
      if (expiryTimer) {
        window.clearTimeout(expiryTimer);
      }
    };
  }, [expiresAt, expireSession, hasHydrated, setSession, t, token, user]);

  return null;
}
