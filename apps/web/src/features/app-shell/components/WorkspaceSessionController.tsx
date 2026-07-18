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
    language,
    hasHydrated,
    hasCheckedSession,
    expireSession,
    completeSessionCheck,
    restoreCookieSession,
    setSession,
  } = useSessionStore();
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );

  useEffect(() => {
    let recoveryPromise: Promise<boolean> | null = null;
    setUnauthorizedHandler(() => {
      if (recoveryPromise) {
        return recoveryPromise;
      }

      recoveryPromise = api.refreshSession()
        .then((session) => {
          setSession(session);
          return true;
        })
        .catch(() => {
          expireSession(t("session.unverified"));
          router.replace("/login");
          return false;
        })
        .finally(() => {
          recoveryPromise = null;
        });

      return recoveryPromise;
    });

    return () => setUnauthorizedHandler(null);
  }, [expireSession, router, setSession, t]);

  useEffect(() => {
    if (!hasHydrated || hasCheckedSession) {
      return;
    }

    let ignore = false;

    async function restoreSession() {
      try {
        const currentUser = await api.meFromCookie();
        if (!ignore) {
          restoreCookieSession(currentUser);
        }
      } catch (error) {
        if (error instanceof ApiError && error.statusCode === 401) {
          try {
            const refreshedSession = await api.refreshSession();
            if (!ignore) {
              setSession(refreshedSession);
            }
            return;
          } catch {
            // The refresh endpoint clears stale auth cookies before login is shown.
          }
        }

        if (!ignore) {
          completeSessionCheck();
        }
      }
    }

    void restoreSession();
    return () => {
      ignore = true;
    };
  }, [completeSessionCheck, hasCheckedSession, hasHydrated, restoreCookieSession, setSession]);

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
      } catch {
        // Authenticated 401 responses are recovered or expired by the shared handler.
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
