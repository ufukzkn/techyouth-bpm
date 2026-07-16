"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api";
import type { Community, TeamPage, User } from "@/lib/types";
import { teamCommunitiesCache, teamPageCache } from "@/features/teams/teamManagementCache";

const pageSize = 6;

type TeamLoadState = "idle" | "loading" | "refreshing" | "error";

export function useTeamManagement({
  activeUser,
  onError,
  token,
}: {
  activeUser: User;
  onError: (message: string) => void;
  token: string | null;
}) {
  const isSuperAdmin = activeUser.role === "SuperAdmin";
  const [communities, setCommunities] = useState<Community[]>(() => teamCommunitiesCache.get(activeUser.id) ?? []);
  const [selectedCommunityId, setSelectedCommunityId] = useState<string | null>(
    isSuperAdmin ? null : activeUser.communityId ?? null,
  );
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [activeFilter, setActiveFilter] = useState<"all" | "active" | "inactive">("all");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<TeamPage | null>(null);
  const [loadState, setLoadState] = useState<TeamLoadState>("loading");
  const [isCommunitiesLoading, setIsCommunitiesLoading] = useState(communities.length === 0);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      setDebouncedQuery(query.trim());
    }, 260);
    return () => window.clearTimeout(timer);
  }, [query]);

  const cacheKey = useMemo(
    () => [activeUser.id, selectedCommunityId ?? "all", debouncedQuery, activeFilter, page].join(":"),
    [activeFilter, activeUser.id, debouncedQuery, page, selectedCommunityId],
  );

  const loadCommunities = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-")) {
      setIsCommunitiesLoading(false);
      return;
    }
    const cached = teamCommunitiesCache.get(activeUser.id);
    if (cached && !force) {
      setCommunities(cached);
      setIsCommunitiesLoading(false);
      return;
    }
    setIsCommunitiesLoading(true);
    try {
      const items = await api.listCommunities(token);
      teamCommunitiesCache.set(activeUser.id, items);
      setCommunities(items);
    } catch {
      onError("Topluluklar yuklenemedi.");
    } finally {
      setIsCommunitiesLoading(false);
    }
  }, [activeUser.id, onError, token]);

  const loadTeams = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-")) {
      setResult({ items: [], page: 1, pageSize, totalCount: 0, unassignedCount: 0 });
      setLoadState("idle");
      return;
    }
    const cached = teamPageCache.get(cacheKey);
    if (cached && !force) {
      setResult(cached);
      setLoadState("idle");
      return;
    }
    setLoadState(force ? "refreshing" : "loading");
    if (!force) {
      setResult(null);
    }
    try {
      const nextResult = await api.listTeams(token, {
        communityId: selectedCommunityId,
        query: debouncedQuery,
        isActive: activeFilter === "all" ? null : activeFilter === "active",
        page,
        pageSize,
      });
      teamPageCache.set(cacheKey, nextResult);
      setResult(nextResult);
      setLoadState("idle");
    } catch {
      setLoadState("error");
      onError("Takimlar yuklenemedi.");
    }
  }, [activeFilter, cacheKey, debouncedQuery, onError, page, selectedCommunityId, token]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCommunities(), 0);
    return () => window.clearTimeout(timer);
  }, [loadCommunities]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadTeams(), 0);
    return () => window.clearTimeout(timer);
  }, [loadTeams]);

  const selectCommunity = useCallback((communityId: string | null) => {
    setSelectedCommunityId(communityId);
    setPage(1);
  }, []);

  const refresh = useCallback(async () => {
    teamPageCache.delete(cacheKey);
    await Promise.all([loadCommunities(true), loadTeams(true)]);
  }, [cacheKey, loadCommunities, loadTeams]);

  return {
    activeFilter,
    communities,
    isCommunitiesLoading,
    isLoading: loadState === "loading",
    isRefreshing: loadState === "refreshing",
    loadState,
    page,
    pageSize,
    query,
    refresh,
    result,
    selectCommunity,
    selectedCommunityId,
    setActiveFilter: (value: "all" | "active" | "inactive") => { setActiveFilter(value); setPage(1); },
    setPage,
    setQuery,
  };
}
