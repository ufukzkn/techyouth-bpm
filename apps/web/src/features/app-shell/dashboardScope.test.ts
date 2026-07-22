import { describe, expect, it } from "vitest";
import { resolvePendingTaskCopy } from "@/features/app-shell/dashboardScope";

describe("dashboard pending task copy", () => {
  it("uses personal copy for a regular user's personal scope", () => {
    expect(resolvePendingTaskCopy("personal", false, false)).toEqual({
      labelKey: "dashboard.pendingTasksPersonal",
      descriptionKey: "dashboard.pendingDescriptionPersonal",
    });
  });

  it("uses managed community copy for Tasks.ManageAll", () => {
    expect(resolvePendingTaskCopy("personal", false, true)).toEqual({
      labelKey: "dashboard.pendingTasksTeam",
      descriptionKey: "dashboard.pendingDescriptionTeam",
    });
  });

  it("uses community copy for the explicit community scope", () => {
    expect(resolvePendingTaskCopy("community", false, true)).toEqual({
      labelKey: "dashboard.pendingTasksCommunity",
      descriptionKey: "dashboard.pendingDescriptionCommunity",
    });
  });

  it("uses global copy for SuperAdmin and the global scope", () => {
    expect(resolvePendingTaskCopy("personal", true, true).labelKey).toBe("dashboard.pendingTasksGlobal");
    expect(resolvePendingTaskCopy("global", true, true).descriptionKey).toBe("dashboard.pendingDescriptionGlobal");
  });
});
