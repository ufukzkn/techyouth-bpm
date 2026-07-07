import { translate, type TranslationKey } from "@/features/i18n/translations";
import { formatApiDateTime } from "@/lib/dateTime";
import type { Language, UserStatus } from "@/lib/types";

export function formatSessionExpiry(expiresAt: string | null, language: Language) {
  return formatApiDateTime(expiresAt, language);
}

export function formatCountdown(totalSeconds: number) {
  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

export function summarizeUserAgent(userAgent: string | null | undefined, language: Language) {
  if (!userAgent) {
    return translate(language, "settings.unknownDevice");
  }

  const browser = userAgent.includes("Edg/")
    ? "Edge"
    : userAgent.includes("Chrome/")
      ? "Chrome"
      : userAgent.includes("Firefox/")
        ? "Firefox"
        : userAgent.includes("Safari/")
          ? "Safari"
          : "Browser";
  const os = userAgent.includes("Windows")
    ? "Windows"
    : userAgent.includes("Mac OS")
      ? "macOS"
      : userAgent.includes("Android")
        ? "Android"
        : userAgent.includes("iPhone") || userAgent.includes("iPad")
          ? "iOS"
          : "Device";

  return `${browser} / ${os}`;
}

export function formatIpAddress(ipAddress: string | null | undefined, language: Language) {
  if (!ipAddress) {
    return translate(language, "settings.unknownIp");
  }

  if (ipAddress === "::1" || ipAddress === "127.0.0.1") {
    return translate(language, "settings.localhostIp");
  }

  if (ipAddress.startsWith("::ffff:")) {
    return ipAddress.replace("::ffff:", "");
  }

  return ipAddress;
}

export function userStatusLabel(language: Language, status: UserStatus) {
  return translate(language, `userStatus.${status}` as TranslationKey);
}
