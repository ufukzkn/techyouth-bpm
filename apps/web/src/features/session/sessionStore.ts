"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { clearProcessBoardCaches } from "@/features/processes/processBoardCache";
import {
  migratePersistedPreferences,
  resolveClientSessionToken,
  selectPersistedPreferences,
  type PersistedPreferences,
} from "@/features/session/sessionPersistence";
import type { BrowserSessionResponse, Language, LoginResponse, ThemeMode, User } from "@/lib/types";

type SessionState = {
  token: string | null;
  user: User | null;
  expiresAt: string | null;
  theme: ThemeMode;
  themePreference: ThemeMode | null;
  language: Language;
  sessionNotice: string | null;
  hasHydrated: boolean;
  hasCheckedSession: boolean;
  setSession: (session: BrowserSessionResponse | LoginResponse) => void;
  restoreCookieSession: (user: User) => void;
  completeSessionCheck: () => void;
  setUser: (user: User) => void;
  expireSession: (message: string) => void;
  clearSessionNotice: () => void;
  setHasHydrated: (hasHydrated: boolean) => void;
  syncSystemTheme: (theme: ThemeMode) => void;
  toggleLanguage: () => void;
  logout: () => void;
  toggleTheme: () => void;
};

function getSystemTheme(): ThemeMode {
  if (typeof window === "undefined") {
    return "light";
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export const useSessionStore = create<SessionState>()(
  persist<SessionState, [], [], PersistedPreferences>(
    (set, get) => ({
      token: null,
      user: null,
      expiresAt: null,
      theme: getSystemTheme(),
      themePreference: null,
      language: "tr",
      sessionNotice: null,
      hasHydrated: false,
      hasCheckedSession: false,
      setSession: (session) => {
        if (get().user?.id && get().user?.id !== session.user.id) {
          clearProcessBoardCaches();
        }
        set({
          token: resolveClientSessionToken("token" in session ? session.token : null),
          user: session.user,
          expiresAt: session.expiresAt,
          sessionNotice: null,
          hasCheckedSession: true,
        });
      },
      restoreCookieSession: (user) => {
        if (get().user?.id && get().user?.id !== user.id) {
          clearProcessBoardCaches();
        }
        set({
          token: resolveClientSessionToken(),
          user,
          expiresAt: null,
          sessionNotice: null,
          hasCheckedSession: true,
        });
      },
      completeSessionCheck: () => set({ hasCheckedSession: true }),
      setUser: (user) => set({ user }),
      expireSession: (message) => {
        clearProcessBoardCaches();
        set({ token: null, user: null, expiresAt: null, sessionNotice: message, hasCheckedSession: true });
      },
      clearSessionNotice: () => set({ sessionNotice: null }),
      setHasHydrated: (hasHydrated) => set({ hasHydrated }),
      syncSystemTheme: (theme) => {
        if (!get().themePreference) {
          set({ theme });
        }
      },
      toggleLanguage: () => set({ language: get().language === "tr" ? "en" : "tr" }),
      logout: () => {
        clearProcessBoardCaches();
        set({ token: null, user: null, expiresAt: null, sessionNotice: null, hasCheckedSession: true });
      },
      toggleTheme: () => {
        const nextTheme = get().theme === "light" ? "dark" : "light";
        set({ theme: nextTheme, themePreference: nextTheme });
      },
    }),
    {
      name: "techyouth-session",
      version: 2,
      partialize: selectPersistedPreferences,
      migrate: (persistedState) => migratePersistedPreferences(persistedState, getSystemTheme()),
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    },
  ),
);
