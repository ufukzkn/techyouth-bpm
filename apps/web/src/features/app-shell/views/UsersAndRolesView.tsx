"use client";

import { RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { sortAuditNewestFirst } from "@/features/app-shell/auditUtils";
import { AccessChangeDialog, SessionRevokeDialog, UserDeleteDialog } from "@/features/app-shell/components/AccessDialogs";
import { ConfirmationDialog } from "@/features/app-shell/components/ConfirmationDialog";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import type { AccessDraft, PendingAccessChange, PendingSessionRevoke, PendingUserDelete, StatusTone } from "@/features/app-shell/types";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Community, CommunityRole, CommunitySummary, Language, Role, SystemAuditLog, User, UserAdmin, UserSession, UserStatus } from "@/lib/types";
import { UserCreatePanel } from "@/features/management/UserCreatePanel";
import { UserDetailPanel } from "@/features/management/UserDetailPanel";
import { UserListPanel } from "@/features/management/UserListPanel";
import { UserManagementFilters } from "@/features/management/UserManagementFilters";
import { clearUserManagementCache, useUserManagement } from "@/features/management/useUserManagement";

const userCommunitySummaryCache = new Map<string, CommunitySummary>();
const allCommunitiesUserCountCache = new Map<string, number>();

export function UsersAndRolesView({
  activeUser,
  language,
  token,
}: {
  activeUser: User;
  language: Language;
  token: string | null;
}) {
  const t = useCallback(
    (key: TranslationKey, values?: Record<string, string | number>) => translate(language, key, values),
    [language],
  );
  const [communities, setCommunities] = useState<Community[]>([]);
  const [communityRoles, setCommunityRoles] = useState<CommunityRole[]>([]);
  const [detailCommunityRoles, setDetailCommunityRoles] = useState<CommunityRole[]>([]);
  const [createCommunityRoles, setCreateCommunityRoles] = useState<CommunityRole[]>([]);
  const [selectedCommunitySummary, setSelectedCommunitySummary] = useState<CommunitySummary | null>(null);
  const [isLoadingCommunitySummary, setIsLoadingCommunitySummary] = useState(false);
  const [isLoadingCommunities, setIsLoadingCommunities] = useState(true);
  const [isLoadingDetailCommunityRoles, setIsLoadingDetailCommunityRoles] = useState(false);
  const [allCommunitiesUserCount, setAllCommunitiesUserCount] = useState<number | null>(null);
  const [isLoadingAllCommunitiesUserCount, setIsLoadingAllCommunitiesUserCount] = useState(false);
  const [selectedCommunityId, setSelectedCommunityId] = useState<string | null>(activeUser.role === "SuperAdmin" ? null : activeUser.communityId ?? null);
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [communityRoleFilter, setCommunityRoleFilter] = useState<string | null>(null);
  const [accessDraft, setAccessDraft] = useState<AccessDraft | null>(null);
  const [pendingAccessChange, setPendingAccessChange] = useState<PendingAccessChange | null>(null);
  const [selectedUserSessions, setSelectedUserSessions] = useState<UserSession[]>([]);
  const [pendingSessionRevoke, setPendingSessionRevoke] = useState<PendingSessionRevoke | null>(null);
  const [pendingUserDelete, setPendingUserDelete] = useState<PendingUserDelete | null>(null);
  const [createUserDraft, setCreateUserDraft] = useState({
    username: "",
    displayName: "",
    email: "",
    role: "User" as Role,
    status: "Active" as UserStatus,
    temporaryPassword: "",
    communityId: activeUser.role === "SuperAdmin" ? "" : activeUser.communityId ?? "",
    communityRoleId: "",
  });
  const [passwordResetDraft, setPasswordResetDraft] = useState({ useManualPassword: false, temporaryPassword: "" });
  const [isResettingPassword, setIsResettingPassword] = useState(false);
  const [isPasswordResetConfirmOpen, setIsPasswordResetConfirmOpen] = useState(false);
  const [passwordResetFeedback, setPasswordResetFeedback] = useState<{ tone: "success" | "error" | "loading"; text: string } | null>(null);
  const [usesCustomTemporaryPassword, setUsesCustomTemporaryPassword] = useState(false);
  const [detailSessionPage, setDetailSessionPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [messageTone, setMessageTone] = useState<StatusTone>("info");
  const [createUserMessage, setCreateUserMessage] = useState<string | null>(null);
  const [createUserMessageTone, setCreateUserMessageTone] = useState<StatusTone>("info");
  const [isLoadingUserLogs, setIsLoadingUserLogs] = useState(false);
  const [isUserLogHistoryOpen, setIsUserLogHistoryOpen] = useState(false);
  const [isLoadingUserSessions, setIsLoadingUserSessions] = useState(false);
  const [isCreatingUser, setIsCreatingUser] = useState(false);
  const [isCreateUserConfirmOpen, setIsCreateUserConfirmOpen] = useState(false);
  const [isCreateUserOpen, setIsCreateUserOpen] = useState(false);
  const selectedCommunity = communities.find((community) => community.id === selectedCommunityId) ?? null;
  const userMessageClassName =
    messageTone === "error" ? "form-error" : messageTone === "success" ? "form-success" : "form-info";
  const createUserMessageClassName =
    createUserMessageTone === "error" ? "form-error" : createUserMessageTone === "success" ? "form-success" : "form-info";

  const showUserMessage = useCallback((nextMessage: string | null, tone: StatusTone = "info") => {
    setMessage(nextMessage);
    setMessageTone(tone);
  }, []);

  const showCreateUserMessage = useCallback((nextMessage: string | null, tone: StatusTone = "info") => {
    setCreateUserMessage(nextMessage);
    setCreateUserMessageTone(tone);
  }, []);

  const handleUserLoadError = useCallback((errorMessage: string) => {
    showUserMessage(errorMessage, "error");
  }, [showUserMessage]);

  const {
    currentPage,
    hasLoaded: hasLoadedUsers,
    isLoading,
    isRefreshing,
    load: loadUsers,
    searchQuery,
    selectedStatuses,
    setPage,
    setSearchQuery,
    setSelectedStatuses,
    toast,
    totalPages,
    users,
  } = useUserManagement({
    communityId: selectedCommunityId,
    communityRoleId: communityRoleFilter,
    language,
    onError: handleUserLoadError,
    refreshFailedText: t("common.refreshFailed"),
    refreshedText: t("common.refreshed"),
    token,
  });

  const loadCommunityContext = useCallback(async () => {
    if (!token || token.startsWith("demo-")) {
      setIsLoadingCommunities(false);
      return;
    }

    setIsLoadingCommunities(true);
    try {
      const communityResult = await api.listCommunities(token);
      setCommunities(communityResult);
      const nextCommunityId = activeUser.role === "SuperAdmin" ? selectedCommunityId : activeUser.communityId ?? null;
      setSelectedCommunityId(nextCommunityId);
      if (nextCommunityId) {
        const roles = await api.listCommunityRoles(token, nextCommunityId);
        setCommunityRoles(roles);
        setCreateUserDraft((draft) => ({
          ...draft,
          communityRoleId: roles.some((role) => role.id === draft.communityRoleId)
            ? draft.communityRoleId
            : getUnassignedRoleId(roles) || roles[0]?.id || "",
        }));
      }
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
    } finally {
      setIsLoadingCommunities(false);
    }
  }, [activeUser.communityId, activeUser.role, language, selectedCommunityId, showUserMessage, t, token]);

  const loadCommunitySummary = useCallback(async (force = false) => {
    if (!token || token.startsWith("demo-") || !selectedCommunityId) {
      setSelectedCommunitySummary(null);
      setIsLoadingCommunitySummary(false);
      return;
    }

    const cached = userCommunitySummaryCache.get(selectedCommunityId);
    if (cached && !force) {
      setSelectedCommunitySummary(cached);
      return;
    }

    setIsLoadingCommunitySummary(true);
    try {
      const summary = await api.getCommunitySummary(token, selectedCommunityId);
      userCommunitySummaryCache.set(selectedCommunityId, summary);
      setSelectedCommunitySummary(summary);
    } catch {
      setSelectedCommunitySummary(null);
    } finally {
      setIsLoadingCommunitySummary(false);
    }
  }, [selectedCommunityId, token]);

  const loadAllCommunitiesUserCount = useCallback(async (force = false) => {
    const shouldLoad = activeUser.role === "SuperAdmin" && !selectedCommunityId;
    if (!token || token.startsWith("demo-") || !shouldLoad) {
      setAllCommunitiesUserCount(null);
      setIsLoadingAllCommunitiesUserCount(false);
      return;
    }

    const cached = allCommunitiesUserCountCache.get("all");
    if (cached !== undefined && !force) {
      setAllCommunitiesUserCount(cached);
      return;
    }

    setIsLoadingAllCommunitiesUserCount(true);
    try {
      const result = await api.listUsers(token, { status: "All", page: 1, pageSize: 1 });
      const count = result.totalCount ?? 0;
      allCommunitiesUserCountCache.set("all", count);
      setAllCommunitiesUserCount(count);
    } finally {
      setIsLoadingAllCommunitiesUserCount(false);
    }
  }, [activeUser.role, selectedCommunityId, token]);

  function refreshUsers() {
    clearUserManagementCache();
    if (selectedCommunityId) {
      userCommunitySummaryCache.delete(selectedCommunityId);
      void loadCommunitySummary(true);
    }
    if (!selectedCommunityId) {
      allCommunitiesUserCountCache.clear();
      void loadAllCommunitiesUserCount(true);
    }
    void loadUsers({ manual: true });
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCommunityContext();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadCommunityContext]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCommunitySummary(), 0);
    return () => window.clearTimeout(timer);
  }, [loadCommunitySummary]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadAllCommunitiesUserCount(), 0);
    return () => window.clearTimeout(timer);
  }, [loadAllCommunitiesUserCount]);

  useEffect(() => {
    async function loadRolesForCommunity() {
      if (!token || token.startsWith("demo-") || !selectedCommunityId) {
        setCommunityRoles([]);
        setCommunityRoleFilter(null);
        return;
      }

      try {
        const roles = await api.listCommunityRoles(token, selectedCommunityId);
        setCommunityRoles(roles);
        setCreateUserDraft((draft) => ({
          ...draft,
          communityRoleId: roles.some((role) => role.id === draft.communityRoleId)
            ? draft.communityRoleId
            : getUnassignedRoleId(roles) || roles[0]?.id || "",
        }));
      } catch (error) {
        showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
      }
    }

    void loadRolesForCommunity();
  }, [language, selectedCommunityId, showUserMessage, t, token]);

  useEffect(() => {
    const communityId = activeUser.role === "SuperAdmin" ? createUserDraft.communityId : activeUser.communityId ?? "";
    const timer = window.setTimeout(() => {
      if (!token || token.startsWith("demo-") || !communityId) {
        setCreateCommunityRoles([]);
        return;
      }

      void api.listCommunityRoles(token, communityId)
        .then((roles) => {
          setCreateCommunityRoles(roles);
          setCreateUserDraft((draft) => ({
            ...draft,
            communityId,
            communityRoleId: roles.some((role) => role.id === draft.communityRoleId)
              ? draft.communityRoleId
              : getUnassignedRoleId(roles) || "",
          }));
        })
        .catch((error) => showCreateUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error"));
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeUser.communityId, activeUser.role, createUserDraft.communityId, language, showCreateUserMessage, t, token]);

  const visibleUsers = users;
  const shouldShowUserSkeleton = !hasLoadedUsers || isLoading;
  const selectedUser = selectedUserId ? users.find((managedUser) => managedUser.id === selectedUserId) ?? null : null;
  const effectiveSelectedUserId = selectedUser?.id ?? null;
  const selectedUsername = selectedUser?.username.toLowerCase();
  const selectedUserLogs = effectiveSelectedUserId
    ? logs
        .filter(
          (log) =>
            log.actorUserId === effectiveSelectedUserId ||
            (log.entityType === "User" && log.entityId === effectiveSelectedUserId) ||
            (selectedUsername ? log.actorUsername.toLowerCase() === selectedUsername : false),
        )
        .sort(sortAuditNewestFirst)
    : [];
  const activeSessionCount = selectedUserSessions.length;
  const detailSessionPageSize = 4;
  const detailSessionTotalPages = Math.max(1, Math.ceil(selectedUserSessions.length / detailSessionPageSize));
  const currentDetailSessionPage = Math.min(detailSessionPage, detailSessionTotalPages);
  const visibleSelectedUserSessions = selectedUserSessions.slice(
    (currentDetailSessionPage - 1) * detailSessionPageSize,
    currentDetailSessionPage * detailSessionPageSize,
  );
  const hasDraftChanges =
    !!selectedUser &&
    !!accessDraft &&
    accessDraft.userId === selectedUser.id &&
    (accessDraft.status !== selectedUser.status
      || accessDraft.communityRoleId !== selectedUser.communityRoleId);

  const loadSelectedUserSessions = useCallback(
    async (userId: string) => {
      if (!token || token.startsWith("demo-")) {
        setSelectedUserSessions([]);
        return;
      }

      setIsLoadingUserSessions(true);
      try {
        const result = await api.listUserSessions(token, userId);
        setSelectedUserSessions(result);
      } catch (error) {
        showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
        setSelectedUserSessions([]);
      } finally {
        setIsLoadingUserSessions(false);
      }
    },
    [language, showUserMessage, t, token],
  );

  const loadSelectedUserLogs = useCallback(
    async (managedUser: UserAdmin) => {
      if (!token || token.startsWith("demo-")) {
        setLogs([]);
        return;
      }

      setIsLoadingUserLogs(true);
      try {
        const result = await api.listSystemAuditLogs(token, {
          query: managedUser.username,
          page: 1,
          pageSize: 50,
        });
        setLogs(result.items ?? []);
      } catch (error) {
        showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
        setLogs([]);
      } finally {
        setIsLoadingUserLogs(false);
      }
    },
    [language, showUserMessage, t, token],
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (!selectedUser) {
        setAccessDraft(null);
        setSelectedUserSessions([]);
        setLogs([]);
        setIsUserLogHistoryOpen(false);
        return;
      }

      setAccessDraft({
        userId: selectedUser.id,
        status: selectedUser.status,
        communityId: selectedUser.communityId,
        communityRoleId: selectedUser.communityRoleId,
      });
      setDetailSessionPage(1);
      setIsUserLogHistoryOpen(false);
      void loadSelectedUserSessions(selectedUser.id);
      void loadSelectedUserLogs(selectedUser);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadSelectedUserLogs, loadSelectedUserSessions, selectedUser]);

  useEffect(() => {
    let ignore = false;
    const detailCommunityId = selectedUser?.communityId;
    if (!token || token.startsWith("demo-") || !detailCommunityId) {
      const timer = window.setTimeout(() => {
        setDetailCommunityRoles([]);
        setIsLoadingDetailCommunityRoles(false);
      }, 0);
      return () => window.clearTimeout(timer);
    }

    const timer = window.setTimeout(() => {
      setIsLoadingDetailCommunityRoles(true);
      void api.listCommunityRoles(token, detailCommunityId)
        .then((roles) => {
          if (!ignore) {
            setDetailCommunityRoles(roles);
          }
        })
        .catch((error) => {
          if (!ignore) {
            setDetailCommunityRoles([]);
            showUserMessage(localizeApiError(error, language, t("settings.loadFailed")), "error");
          }
        })
        .finally(() => {
          if (!ignore) {
            setIsLoadingDetailCommunityRoles(false);
          }
        });
    }, 0);

    return () => {
      ignore = true;
      window.clearTimeout(timer);
    };
  }, [language, selectedUser?.communityId, showUserMessage, t, token]);

  async function updateUserAccess(
    userId: string,
    status: UserStatus,
    communityId?: string | null,
    communityRoleId?: string | null,
  ) {
    if (!token) {
      return;
    }

    try {
      await api.updateUserAccess(token, userId, status, communityId, communityRoleId);
      clearUserManagementCache();
      allCommunitiesUserCountCache.clear();
      if (!selectedCommunityId) {
        void loadAllCommunitiesUserCount(true);
      }
      if (communityId) {
        userCommunitySummaryCache.delete(communityId);
        await loadCommunitySummary(true);
      }
      await loadUsers();
      showUserMessage(t("settings.userAccessUpdated"), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.userAccessFailed")), "error");
    }
  }

  async function createUser() {
    if (!token) {
      return;
    }

    const payload = {
      ...createUserDraft,
      role: "User" as Role,
      temporaryPassword: usesCustomTemporaryPassword ? createUserDraft.temporaryPassword : "",
      communityId: createUserDraft.communityId || null,
      communityRoleId: createUserDraft.communityRoleId || getUnassignedRoleId(createCommunityRoles),
    };

    setIsCreatingUser(true);
    showCreateUserMessage(null);
    try {
      const createdUser = await api.createUser(token, payload);
      setCreateUserDraft({
        username: "",
        displayName: "",
        email: "",
        role: "User",
        status: "Active",
        temporaryPassword: "",
        communityId: activeUser.role === "SuperAdmin" ? "" : activeUser.communityId ?? "",
        communityRoleId: getUnassignedRoleId(createCommunityRoles),
      });
      setUsesCustomTemporaryPassword(false);
      clearUserManagementCache();
      allCommunitiesUserCountCache.clear();
      if (!selectedCommunityId) {
        void loadAllCommunitiesUserCount(true);
      }
      await loadUsers();
      setSelectedUserId(createdUser.id);
      showCreateUserMessage(t("users.userCreated", { username: createdUser.username }), "success");
    } catch (error) {
      showCreateUserMessage(localizeApiError(error, language, t("users.userCreateFailed")), "error");
    } finally {
      setIsCreatingUser(false);
    }
  }

  function selectUser(managedUser: UserAdmin) {
    setSelectedUserId(managedUser.id);
  }

  function requestUserAccessChange(managedUser: UserAdmin, status: UserStatus, communityRoleId?: string | null) {
    if (managedUser.status === status && managedUser.communityRoleId === communityRoleId) {
      return;
    }

    showUserMessage(null);
    setPendingAccessChange({
      userId: managedUser.id,
      displayName: managedUser.displayName,
      username: managedUser.username,
      fromStatus: managedUser.status,
      toStatus: status,
      fromCommunityRoleName: managedUser.communityRoleName,
      toCommunityRoleName: detailCommunityRoles.find((communityRole) => communityRole.id === communityRoleId)?.name,
    });
  }

  function requestDraftAccessChange() {
    if (!selectedUser || !accessDraft || !hasDraftChanges) {
      return;
    }

    requestUserAccessChange(selectedUser, accessDraft.status, accessDraft.communityRoleId);
  }

  async function confirmUserAccessChange() {
    if (!pendingAccessChange) {
      return;
    }

    const change = pendingAccessChange;
    setPendingAccessChange(null);
    await updateUserAccess(change.userId, change.toStatus, accessDraft?.communityId ?? selectedUser?.communityId, accessDraft?.communityRoleId);
  }

  async function resetSelectedUserPassword() {
    if (!token || !selectedUser || activeUser.role !== "SuperAdmin") {
      return;
    }

    setIsResettingPassword(true);
    setPasswordResetFeedback({ tone: "loading", text: "Geçici şifre hazırlaniyor..." });
    try {
      await api.resetUserPasswordByAdmin(token, selectedUser.id, {
        useManualPassword: passwordResetDraft.useManualPassword,
        temporaryPassword: passwordResetDraft.useManualPassword ? passwordResetDraft.temporaryPassword : null,
      });
      setPasswordResetDraft({ useManualPassword: false, temporaryPassword: "" });
      clearUserManagementCache();
      await loadUsers();
      setPasswordResetFeedback({ tone: "success", text: "Geçici şifre e-posta ile gönderildi." });
    } catch (error) {
      setPasswordResetFeedback({ tone: "error", text: localizeApiError(error, language, "Şifre sıfırlanamadı.") });
    } finally {
      setIsResettingPassword(false);
    }
  }

  function requestSessionRevoke(session: UserSession) {
    if (!selectedUser) {
      return;
    }

    setPendingSessionRevoke({
      userId: selectedUser.id,
      sessionId: session.id,
      displayName: selectedUser.displayName,
      username: selectedUser.username,
      expiresAt: session.expiresAt,
      isCurrent: session.isCurrent && selectedUser.id === activeUser.id,
    });
  }

  async function confirmSessionRevoke() {
    if (!pendingSessionRevoke || !token) {
      return;
    }

    const revoke = pendingSessionRevoke;
    setPendingSessionRevoke(null);
    try {
      await api.revokeUserSession(token, revoke.userId, revoke.sessionId);
      await loadSelectedUserSessions(revoke.userId);
      clearUserManagementCache();
      await loadUsers();
      showUserMessage(t("settings.sessionRevoked"), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("settings.sessionRevokeFailed")), "error");
    }
  }

  function requestUserDelete(managedUser: UserAdmin) {
    setPendingUserDelete({
      userId: managedUser.id,
      displayName: managedUser.displayName,
      username: managedUser.username,
    });
  }

  async function confirmUserDelete() {
    if (!pendingUserDelete || !token) {
      return;
    }

    const deletion = pendingUserDelete;
    setPendingUserDelete(null);
    try {
      await api.deleteUser(token, deletion.userId);
      setSelectedUserId(null);
      setSelectedUserSessions([]);
      clearUserManagementCache();
      allCommunitiesUserCountCache.clear();
      if (!selectedCommunityId) {
        void loadAllCommunitiesUserCount(true);
      }
      await loadUsers();
      showUserMessage(t("users.userDeleted", { username: deletion.username }), "success");
    } catch (error) {
      showUserMessage(localizeApiError(error, language, t("users.userDeleteFailed")), "error");
    }
  }

  return (
    <section className="settings-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{t("users.eyebrow")}</span>
          <h2>{t("users.title")}</h2>
        </div>
        <div className="section-heading-actions">
          <p>{t("users.description")}</p>
          <button
            className="secondary-button refresh-button"
            disabled={isRefreshing}
            type="button"
            onClick={refreshUsers}
          >
            <RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={17} />
            {isRefreshing ? t("common.refreshing") : t("common.refresh")}
          </button>
        </div>
      </div>

      <UserManagementFilters
        activeUser={activeUser}
        allCommunitiesUserCount={allCommunitiesUserCount}
        communities={communities}
        communityRoleFilter={communityRoleFilter}
        communityRoles={communityRoles}
        isLoadingAllCommunitiesUserCount={isLoadingAllCommunitiesUserCount}
        isLoadingCommunities={isLoadingCommunities}
        isLoadingCommunitySummary={isLoadingCommunitySummary}
        language={language}
        message={message}
        messageClassName={userMessageClassName}
        onCommunityChange={(communityId) => {
          setSelectedCommunityId(communityId);
          setCommunityRoleFilter(null);
          setPage(1);
          setSelectedUserId(null);
        }}
        onRoleFilterChange={(communityRoleId) => {
          setCommunityRoleFilter(communityRoleId);
          setPage(1);
        }}
        onSearchChange={(query) => {
          setSearchQuery(query);
          setPage(1);
        }}
        onToggleStatus={(status) => {
          setSelectedStatuses((current) =>
            current.includes(status)
              ? current.filter((item) => item !== status)
              : [...current, status],
          );
          setPage(1);
        }}
        searchQuery={searchQuery}
        selectedCommunity={selectedCommunity}
        selectedCommunityId={selectedCommunityId}
        selectedCommunitySummary={selectedCommunitySummary}
        selectedStatuses={selectedStatuses}
        t={t}
      />

      <div className="management-layout">
        <div className="management-left-column">
        <UserListPanel
          currentPage={currentPage}
          isLoading={shouldShowUserSkeleton}
          language={language}
          onNextPage={() => setPage((value) => Math.min(value + 1, totalPages))}
          onPageChange={setPage}
          onPreviousPage={() => setPage((value) => Math.max(value - 1, 1))}
          onSelect={selectUser}
          selectedUserId={selectedUserId}
          t={t}
          totalPages={totalPages}
          users={visibleUsers}
        />
        <UserCreatePanel
          activeUser={activeUser}
          communities={communities}
          communityRoles={createCommunityRoles}
          draft={createUserDraft}
          isCreating={isCreatingUser}
          isOpen={isCreateUserOpen}
          message={createUserMessage}
          messageClassName={createUserMessageClassName}
          onDraftChange={(patch) =>
            setCreateUserDraft((draft) => ({ ...draft, ...patch }))
          }
          onRequestCreate={() => setIsCreateUserConfirmOpen(true)}
          onToggleCustomPassword={(enabled) => {
            setUsesCustomTemporaryPassword(enabled);
            if (!enabled) {
              setCreateUserDraft((draft) => ({ ...draft, temporaryPassword: "" }));
            }
          }}
          onToggleOpen={() => {
            setIsCreateUserOpen((isOpen) => !isOpen);
            showCreateUserMessage(null);
          }}
          t={t}
          usesCustomTemporaryPassword={usesCustomTemporaryPassword}
        />
        </div>

        <UserDetailPanel
          accessDraft={accessDraft}
          activeSessionCount={activeSessionCount}
          activeUser={activeUser}
          currentSessionPage={currentDetailSessionPage}
          detailCommunityRoles={detailCommunityRoles}
          hasDraftChanges={hasDraftChanges}
          isLoadingDetailCommunityRoles={isLoadingDetailCommunityRoles}
          isLoadingUserLogs={isLoadingUserLogs}
          isLoadingUserSessions={isLoadingUserSessions}
          isPasswordResetting={isResettingPassword}
          isUserLogHistoryOpen={isUserLogHistoryOpen}
          language={language}
          onAccessDraftChange={setAccessDraft}
          onClose={() => setSelectedUserId(null)}
          onDelete={requestUserDelete}
          onHistoryToggle={() => setIsUserLogHistoryOpen((isOpen) => !isOpen)}
          onNextSessionPage={() =>
            setDetailSessionPage((value) => Math.min(value + 1, detailSessionTotalPages))
          }
          onPasswordReset={() => setIsPasswordResetConfirmOpen(true)}
          onPreviousSessionPage={() =>
            setDetailSessionPage((value) => Math.max(value - 1, 1))
          }
          onRefreshUsers={refreshUsers}
          onRequestAccessChange={requestDraftAccessChange}
          onSessionPageChange={setDetailSessionPage}
          onSessionRevoke={requestSessionRevoke}
          passwordResetFeedback={passwordResetFeedback}
          selectedUser={selectedUser}
          selectedUserLogs={selectedUserLogs}
          selectedUserSessions={selectedUserSessions}
          sessionTotalPages={detailSessionTotalPages}
          t={t}
          token={token}
          visibleSessions={visibleSelectedUserSessions}
        />
      </div>
      {pendingAccessChange ? (
        <AccessChangeDialog
          change={pendingAccessChange}
          language={language}
          onCancel={() => setPendingAccessChange(null)}
          onConfirm={confirmUserAccessChange}
        />
      ) : null}
      {pendingSessionRevoke ? (
        <SessionRevokeDialog
          revoke={pendingSessionRevoke}
          language={language}
          onCancel={() => setPendingSessionRevoke(null)}
          onConfirm={confirmSessionRevoke}
        />
      ) : null}
      {pendingUserDelete ? (
        <UserDeleteDialog
          deletion={pendingUserDelete}
          language={language}
          onCancel={() => setPendingUserDelete(null)}
          onConfirm={confirmUserDelete}
        />
      ) : null}
      {isPasswordResetConfirmOpen && selectedUser ? (
        <ConfirmationDialog
          eyebrow="Şifre sıfırlama"
          title={`${selectedUser.displayName} için geçici şifre gönderilsin mi?`}
          description="Varsayılan akışta güçlü bir geçici şifre üretilir, e-posta ile gönderilir ve kullanıcı ilk girişte şifresini değiştirmek zorunda kalır."
          confirmLabel={passwordResetDraft.useManualPassword ? "Manuel şifreyi uygula" : "Geçici şifre gönder"}
          onCancel={() => {
            setIsPasswordResetConfirmOpen(false);
            setPasswordResetDraft({ useManualPassword: false, temporaryPassword: "" });
          }}
          onConfirm={() => {
            setIsPasswordResetConfirmOpen(false);
            void resetSelectedUserPassword();
          }}
        >
          <label className="checkbox-line custom-password-toggle compact-password-toggle">
            <input
              checked={passwordResetDraft.useManualPassword}
              onChange={(event) => setPasswordResetDraft((draft) => ({ ...draft, useManualPassword: event.target.checked, temporaryPassword: event.target.checked ? draft.temporaryPassword : "" }))}
              type="checkbox"
            />
            <span>Manuel şifre belirle (tavsiye edilmez)</span>
          </label>
          {passwordResetDraft.useManualPassword ? <input className="inline-password-input" value={passwordResetDraft.temporaryPassword} onChange={(event) => setPasswordResetDraft((draft) => ({ ...draft, temporaryPassword: event.target.value }))} placeholder="En az 8 karakter" type="password" /> : null}
        </ConfirmationDialog>
      ) : null}
      {isCreateUserConfirmOpen ? (
        <ConfirmationDialog
          eyebrow="Kullanıcı oluşturma"
          title={`${createUserDraft.username || "Yeni kullanıcı"} oluşturulsun mu?`}
          description="Kullanıcı seçilen topluluk ve role bağlanır. Geçici şifre e-posta ile iletilir ve ilk girişte şifre değişimi zorunlu olur."
          confirmLabel="Kullanıcıyı oluştur"
          tone="primary"
          onCancel={() => setIsCreateUserConfirmOpen(false)}
          onConfirm={() => {
            setIsCreateUserConfirmOpen(false);
            void createUser();
          }}
        />
      ) : null}
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function getUnassignedRoleId(roles: CommunityRole[]) {
  return roles.find((role) => role.templateKey === "unassigned")?.id
    ?? roles.find((role) => ["atanmadı", "atanmadi"].includes(role.name.toLocaleLowerCase("tr-TR")))?.id
    ?? "";
}
