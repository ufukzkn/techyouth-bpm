import type { TranslationKey } from "@/features/i18n/translations";
import type { WorkflowVisibilityScope } from "@/lib/types";

type PendingTaskCopy = {
  labelKey: TranslationKey;
  descriptionKey: TranslationKey;
};

export function resolvePendingTaskCopy(
  scope: WorkflowVisibilityScope,
  isSuperAdmin: boolean,
  managesAllTasks: boolean,
): PendingTaskCopy {
  if (scope === "global" || (scope === "personal" && isSuperAdmin)) {
    return {
      labelKey: "dashboard.pendingTasksGlobal",
      descriptionKey: "dashboard.pendingDescriptionGlobal",
    };
  }

  if (scope === "community") {
    return {
      labelKey: "dashboard.pendingTasksCommunity",
      descriptionKey: "dashboard.pendingDescriptionCommunity",
    };
  }

  if (managesAllTasks) {
    return {
      labelKey: "dashboard.pendingTasksTeam",
      descriptionKey: "dashboard.pendingDescriptionTeam",
    };
  }

  return {
    labelKey: "dashboard.pendingTasksPersonal",
    descriptionKey: "dashboard.pendingDescriptionPersonal",
  };
}
