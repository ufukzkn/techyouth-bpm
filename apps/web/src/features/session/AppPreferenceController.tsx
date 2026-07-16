"use client";

import { useEffect } from "react";
import { useSessionStore } from "@/features/session/sessionStore";

export function AppPreferenceController() {
  const language = useSessionStore((state) => state.language);
  const theme = useSessionStore((state) => state.theme);
  const syncSystemTheme = useSessionStore((state) => state.syncSystemTheme);

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

  return null;
}
