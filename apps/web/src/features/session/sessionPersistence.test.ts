import { describe, expect, it } from "vitest";
import {
  COOKIE_SESSION_MARKER,
  migratePersistedPreferences,
  resolveClientSessionToken,
  selectPersistedPreferences,
} from "@/features/session/sessionPersistence";

describe("session persistence", () => {
  it("keeps only the harmless demo marker and replaces real tokens with the cookie marker", () => {
    expect(resolveClientSessionToken("demo-admin")).toBe("demo-admin");
    expect(resolveClientSessionToken("raw-access-token")).toBe(COOKIE_SESSION_MARKER);
    expect(resolveClientSessionToken()).toBe(COOKIE_SESSION_MARKER);
  });

  it("persists preferences without auth identity or token data", () => {
    const state = {
      theme: "dark" as const,
      themePreference: "dark" as const,
      language: "en" as const,
      token: "raw-access-token",
      csrfToken: "csrf-token",
      user: { id: "user-id" },
      expiresAt: "2026-07-18T12:00:00Z",
    };

    expect(selectPersistedPreferences(state)).toEqual({
      theme: "dark",
      themePreference: "dark",
      language: "en",
    });
  });

  it("migrates legacy snapshots without carrying sensitive session fields forward", () => {
    const migrated = migratePersistedPreferences({
      token: "legacy-token",
      csrfToken: "legacy-csrf",
      user: { id: "legacy-user" },
      expiresAt: "2026-07-18T12:00:00Z",
      theme: "light",
      themePreference: null,
      language: "tr",
    }, "dark");

    expect(migrated).toEqual({
      theme: "dark",
      themePreference: null,
      language: "tr",
    });
    expect(migrated).not.toHaveProperty("token");
    expect(migrated).not.toHaveProperty("user");
  });
});
