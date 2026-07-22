import { describe, expect, it } from "vitest";

import { translations } from "./translations";

describe("translations", () => {
  it("keeps Turkish and English translation keys in parity", () => {
    expect(Object.keys(translations.tr).sort()).toEqual(Object.keys(translations.en).sort());
  });

  it("keeps technical keys ASCII and stable", () => {
    expect(Object.keys(translations.tr).every((key) => /^[\x20-\x7E]+$/.test(key))).toBe(true);
  });

  it("uses the request wording for the dashboard runner action", () => {
    expect(translations.tr["dashboard.quick.start"]).toBe("Yeni Talep");
    expect(translations.en["dashboard.quick.start"]).toBe("New request");
  });
});
