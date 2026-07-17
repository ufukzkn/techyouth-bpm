import { translate, type TranslationKey } from "@/features/i18n/translations";
import type { Language, SystemAuditLog } from "@/lib/types";
import type { AuditCategory, AuditHistoryMode } from "@/features/app-shell/types";

export function getFocusedAuditLogs(
  logs: SystemAuditLog[],
  selectedLog: SystemAuditLog,
  selectedLogCategory: Exclude<AuditCategory, "all">,
  mode: AuditHistoryMode,
) {
  return logs
    .filter((log) => {
      const sameActor = Boolean(selectedLog.actorUserId && log.actorUserId === selectedLog.actorUserId);
      const sameEntity = Boolean(
        selectedLog.entityId && log.entityId === selectedLog.entityId && log.entityType === selectedLog.entityType,
      );
      const targetUserAsActor = Boolean(
        selectedLog.entityType === "User" && selectedLog.entityId && log.actorUserId === selectedLog.entityId,
      );

      if (mode === "actor") {
        return sameActor;
      }

      if (mode === "target") {
        return sameEntity || targetUserAsActor;
      }

      if (selectedLog.entityId) {
        return sameActor && sameEntity;
      }

      return getAuditCategory(log) === selectedLogCategory && sameActor;
    })
    .sort(sortAuditNewestFirst);
}

export function sortAuditNewestFirst(left: SystemAuditLog, right: SystemAuditLog) {
  return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
}

export function getAuditHistoryTitle(
  log: SystemAuditLog,
  mode: AuditHistoryMode,
  language: Language,
) {
  if (mode === "actor") {
    return translate(language, "logs.actorHistoryTitle", { value: log.actorUsername });
  }

  if (mode === "target") {
    return translate(language, "logs.targetHistoryTitle", { value: getAuditTargetLabel(log) });
  }

  return translate(language, "logs.relatedHistoryTitle");
}

export function getAuditTargetLabel(log: SystemAuditLog) {
  if (log.entityType === "User" && log.entityId) {
    return log.entityUsername ?? log.entityDisplayName ?? log.entityId;
  }

  const match = log.description.match(/(?:user)\s+'([^']+)'/i);
  return match?.[1] ?? log.entityId ?? log.entityType;
}

export function getAuditCountCacheKey(query: string) {
  return query.toLowerCase();
}

export function getAuditCategory(log: SystemAuditLog): Exclude<AuditCategory, "all"> {
  return log.category === "access"
    || log.category === "forms"
    || log.category === "processes"
    || log.category === "tasks"
    ? log.category
    : "identity";
}

export function formatAuditAction(action: string, language: Language) {
  const key = `logs.action.${action}` as TranslationKey;
  const translated = translate(language, key);
  return translated === key ? action : translated;
}

export function buildAuditSearchText(log: SystemAuditLog, language: Language) {
  return [
    log.actorDisplayName,
    log.actorUsername,
    log.action,
    formatAuditAction(log.action, language),
    log.entityType,
    log.entityId ?? "",
    log.description,
  ]
    .join(" ")
    .toLowerCase();
}
