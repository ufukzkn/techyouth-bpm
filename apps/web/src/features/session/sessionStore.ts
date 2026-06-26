"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { LoginResponse, ThemeMode, User } from "@/lib/types";

type SessionState = {
  token: string | null;
  user: User | null;
  expiresAt: string | null;
  theme: ThemeMode;
  sessionNotice: string | null;
  hasHydrated: boolean;
  setSession: (session: LoginResponse) => void;
  expireSession: (message: string) => void;
  clearSessionNotice: () => void;
  setHasHydrated: (hasHydrated: boolean) => void;
  logout: () => void;
  toggleTheme: () => void;
};

export const useSessionStore = create<SessionState>()(
  persist(
    (set, get) => ({
      token: null,
      user: null,
      expiresAt: null,
      theme: "light",
      sessionNotice: null,
      hasHydrated: false,
      setSession: (session) =>
        set({
          token: session.token,
          user: session.user,
          expiresAt: session.expiresAt,
          sessionNotice: null,
        }),
      expireSession: (message) => set({ token: null, user: null, expiresAt: null, sessionNotice: message }),
      clearSessionNotice: () => set({ sessionNotice: null }),
      setHasHydrated: (hasHydrated) => set({ hasHydrated }),
      logout: () => set({ token: null, user: null, expiresAt: null, sessionNotice: null }),
      toggleTheme: () => set({ theme: get().theme === "light" ? "dark" : "light" }),
    }),
    {
      name: "techyouth-session",
      partialize: (state) => ({
        token: state.token,
        user: state.user,
        expiresAt: state.expiresAt,
        theme: state.theme,
      }),
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    },
  ),
);
