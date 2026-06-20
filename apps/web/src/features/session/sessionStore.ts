"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { LoginResponse, ThemeMode, User } from "@/lib/types";

type SessionState = {
  token: string | null;
  user: User | null;
  theme: ThemeMode;
  setSession: (session: LoginResponse) => void;
  logout: () => void;
  toggleTheme: () => void;
};

export const useSessionStore = create<SessionState>()(
  persist(
    (set, get) => ({
      token: null,
      user: null,
      theme: "light",
      setSession: (session) => set({ token: session.token, user: session.user }),
      logout: () => set({ token: null, user: null }),
      toggleTheme: () => set({ theme: get().theme === "light" ? "dark" : "light" }),
    }),
    {
      name: "techyouth-session",
      partialize: (state) => ({
        token: state.token,
        user: state.user,
        theme: state.theme,
      }),
    },
  ),
);
