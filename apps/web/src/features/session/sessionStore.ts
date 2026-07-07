"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Language, LoginResponse, ThemeMode, User } from "@/lib/types";

type SessionState = {
  token: string | null;
  user: User | null;
  expiresAt: string | null;
  theme: ThemeMode;
  themePreference: ThemeMode | null;
  language: Language;
  sessionNotice: string | null;
  hasHydrated: boolean;
  setSession: (session: LoginResponse) => void;
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
  persist(
    (set, get) => ({
      token: null,
      user: null,
      expiresAt: null,
      theme: getSystemTheme(),
      themePreference: null,
      language: "tr",
      sessionNotice: null,
      hasHydrated: false,
      setSession: (session) =>
        set({
          token: session.token,
          user: session.user,
          expiresAt: session.expiresAt,
          sessionNotice: null,
        }),
      setUser: (user) => set({ user }),
      expireSession: (message) => set({ token: null, user: null, expiresAt: null, sessionNotice: message }),
      clearSessionNotice: () => set({ sessionNotice: null }),
      setHasHydrated: (hasHydrated) => set({ hasHydrated }),
      syncSystemTheme: (theme) => {
        if (!get().themePreference) {
          set({ theme });
        }
      },
      toggleLanguage: () => set({ language: get().language === "tr" ? "en" : "tr" }),
      logout: () => set({ token: null, user: null, expiresAt: null, sessionNotice: null }),
      toggleTheme: () => {
        const nextTheme = get().theme === "light" ? "dark" : "light";
        set({ theme: nextTheme, themePreference: nextTheme });
      },
    }),
    {
      name: "techyouth-session",
      version: 1,
      partialize: (state) => ({
        token: state.token,
        user: state.user,
        expiresAt: state.expiresAt,
        theme: state.theme,
        themePreference: state.themePreference,
        language: state.language,
      }),
      migrate: (persistedState) => {
        if (!persistedState || typeof persistedState !== "object") {
          return persistedState;
        }

        const state = persistedState as Partial<SessionState>;
        return {
          ...state,
          theme: state.themePreference ?? getSystemTheme(),
          themePreference: state.themePreference ?? null,
          language: state.language ?? "tr",
        };
      },
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    },
  ),
);
