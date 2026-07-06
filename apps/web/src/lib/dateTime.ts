import { translate } from "@/features/i18n/translations";
import type { Language } from "@/lib/types";

export function parseApiDateTime(value: string) {
  const hasTimeZone = /(?:z|[+-]\d{2}:\d{2})$/i.test(value);
  return new Date(hasTimeZone ? value : `${value}Z`);
}

export function formatApiDateTime(value: string | null | undefined, language: Language) {
  if (!value) {
    return translate(language, "session.noExpiry");
  }

  const date = parseApiDateTime(value);
  if (Number.isNaN(date.getTime())) {
    return translate(language, "session.unknownExpiry");
  }

  const formatted = date.toLocaleString(language === "tr" ? "tr-TR" : "en-US", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Europe/Istanbul",
  });
  return `${formatted} GMT+3`;
}
