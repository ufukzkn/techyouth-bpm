import type { Language, ThemeMode } from "@/lib/types";

export const COOKIE_SESSION_MARKER = "cookie-session";

export type PersistedPreferences = {
  theme: ThemeMode;
  themePreference: ThemeMode | null;
  language: Language;
};

export function resolveClientSessionToken(rawToken?: string | null) {
  return rawToken?.startsWith("demo-") ? rawToken : COOKIE_SESSION_MARKER;
}

export function selectPersistedPreferences(state: PersistedPreferences): PersistedPreferences {
  return {
    theme: state.theme,
    themePreference: state.themePreference,
    language: state.language,
  };
}

export function migratePersistedPreferences(
  persistedState: unknown,
  systemTheme: ThemeMode,
): PersistedPreferences {
  const state = persistedState && typeof persistedState === "object"
    ? persistedState as Partial<PersistedPreferences>
    : {};
  const themePreference = state.themePreference === "light" || state.themePreference === "dark"
    ? state.themePreference
    : null;

  return {
    theme: themePreference ?? systemTheme,
    themePreference,
    language: state.language === "en" ? "en" : "tr",
  };
}
