import { useCallback, useEffect, useState } from "react";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { api } from "@/lib/api";
import type { Language, UserAdmin, UserStatus } from "@/lib/types";

const minimumRefreshDelayMs = 500;
const pageSize = 4;
const userPageCache = new Map<string, { items: UserAdmin[]; totalCount: number }>();

export function clearUserManagementCache() {
  userPageCache.clear();
}

export function useUserManagement({
  communityId,
  communityRoleId,
  language,
  onError,
  refreshFailedText,
  refreshedText,
  token,
}: {
  communityId: string | null;
  communityRoleId: string | null;
  language: Language;
  onError: (message: string) => void;
  refreshFailedText: string;
  refreshedText: string;
  token: string | null;
}) {
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [totalUsers, setTotalUsers] = useState(0);
  const [searchQuery, setSearchQueryState] = useState("");
  const [selectedStatuses, setSelectedStatusesState] = useState<UserStatus[]>(["PendingApproval"]);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);

  const load = useCallback(async ({ manual = false }: { manual?: boolean } = {}) => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    const startedAt = Date.now();
    const statusKey = [...selectedStatuses].sort().join(",") || "all";
    const cacheKey = [communityId ?? "all", communityRoleId ?? "all", statusKey, searchQuery.trim(), page, pageSize].join("|");
    const cached = userPageCache.get(cacheKey);
    if (cached && !manual) {
      setUsers(cached.items);
      setTotalUsers(cached.totalCount);
      setHasLoaded(true);
      return;
    }

    if (manual) {
      setIsRefreshing(true);
    } else {
      setIsLoading(true);
    }
    try {
      const result = await api.listUsers(token, {
        communityId,
        communityRoleId,
        page,
        pageSize,
        query: searchQuery.trim(),
        statuses: selectedStatuses,
      });
      const next = { items: result.items ?? [], totalCount: result.totalCount ?? 0 };
      userPageCache.set(cacheKey, next);
      setUsers(next.items);
      setTotalUsers(next.totalCount);
      setHasLoaded(true);
      if (manual) {
        await waitForMinimumDelay(startedAt, minimumRefreshDelayMs);
        setToast({ kind: "success", text: refreshedText });
      }
    } catch (error) {
      const message = localizeApiError(error, language, refreshFailedText);
      setHasLoaded(true);
      if (manual) {
        await waitForMinimumDelay(startedAt, minimumRefreshDelayMs);
        setToast({ kind: "error", text: message });
      } else {
        onError(message);
      }
    } finally {
      if (manual) {
        setIsRefreshing(false);
      } else {
        setIsLoading(false);
      }
    }
  }, [communityId, communityRoleId, language, onError, page, refreshFailedText, refreshedText, searchQuery, selectedStatuses, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 250);
    return () => window.clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    if (!toast) {
      return;
    }
    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  function setSearchQuery(value: string) {
    setSearchQueryState(value);
    setPage(1);
  }

  function setSelectedStatuses(value: UserStatus[] | ((current: UserStatus[]) => UserStatus[])) {
    setSelectedStatusesState(value);
    setPage(1);
  }

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  return {
    currentPage: Math.min(page, totalPages),
    hasLoaded,
    isLoading,
    isRefreshing,
    load,
    page,
    searchQuery,
    selectedStatuses,
    setPage,
    setSearchQuery,
    setSelectedStatuses,
    toast,
    totalPages,
    totalUsers,
    users,
  };
}

function waitForMinimumDelay(startedAt: number, minimumDelayMs: number) {
  const remainingMs = minimumDelayMs - (Date.now() - startedAt);
  return remainingMs > 0 ? new Promise<void>((resolve) => window.setTimeout(resolve, remainingMs)) : Promise.resolve();
}
