import { Filter, History, RefreshCw, Search, ShieldCheck, Sparkles, UserCog, UserPlus, X } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { sortAuditNewestFirst } from "@/features/app-shell/auditUtils";
import { AccessChangeDialog, SessionRevokeDialog, UserDeleteDialog } from "@/features/app-shell/components/AccessDialogs";
import { PaginationControls } from "@/features/app-shell/components/PaginationControls";
import { SystemAuditTimeline } from "@/features/app-shell/components/SystemAuditTimeline";
import { WorkspaceToast } from "@/features/app-shell/components/WorkspaceToast";
import { formatIpAddress, formatSessionExpiry, summarizeUserAgent, userStatusLabel } from "@/features/app-shell/sessionFormatters";
import type { AccessDraft, PendingAccessChange, PendingSessionRevoke, PendingUserDelete, StatusTone } from "@/features/app-shell/types";
import { localizeApiError } from "@/features/i18n/apiErrorMessages";
import { roleLabel, translate, type TranslationKey } from "@/features/i18n/translations";
import { api } from "@/lib/api";
import type { Language, Role, SystemAuditLog, User, UserAdmin, UserSession, UserStatus } from "@/lib/types";

const minimumRefreshDelayMs = 500;

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
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [logs, setLogs] = useState<SystemAuditLog[]>([]);
  const [totalUsers, setTotalUsers] = useState(0);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<UserStatus | "All">("PendingApproval");
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
  });
  const [usesCustomTemporaryPassword, setUsesCustomTemporaryPassword] = useState(false);
  const [page, setPage] = useState(1);
  const [detailSessionPage, setDetailSessionPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [messageTone, setMessageTone] = useState<StatusTone>("info");
  const [createUserMessage, setCreateUserMessage] = useState<string | null>(null);
  const [createUserMessageTone, setCreateUserMessageTone] = useState<StatusTone>("info");
  const [isLoading, setIsLoading] = useState(false);
  const [hasLoadedUsers, setHasLoadedUsers] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [isLoadingUserLogs, setIsLoadingUserLogs] = useState(false);
  const [isLoadingUserSessions, setIsLoadingUserSessions] = useState(false);
  const [isCreatingUser, setIsCreatingUser] = useState(false);
  const [isCreateUserOpen, setIsCreateUserOpen] = useState(false);
  const pageSize = 4;
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

  useEffect(() => {
    if (!toast) {
      return;
    }

    const timer = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const loadUsers = useCallback(async (options: { manual?: boolean } = {}) => {
    if (!token || token.startsWith("demo-")) {
      return;
    }

    const query = searchQuery.trim();
    const isManualRefresh = options.manual === true;
    const refreshStartedAt = Date.now();
    if (isManualRefresh) {
      setIsRefreshing(true);
    } else {
      setIsLoading(true);
    }

    try {
      const userResult = await api.listUsers(token, {
        query,
        status: statusFilter,
        page,
        pageSize,
      });
      setUsers(userResult.items ?? []);
      setTotalUsers(userResult.totalCount ?? 0);
      setHasLoadedUsers(true);
      if (isManualRefresh) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setToast({ kind: "success", text: t("common.refreshed") });
      }
    } catch (error) {
      const errorMessage = localizeApiError(error, language, t("common.refreshFailed"));
      if (isManualRefresh) {
        await waitForMinimumDelay(refreshStartedAt, minimumRefreshDelayMs);
        setToast({ kind: "error", text: errorMessage });
      } else {
        showUserMessage(errorMessage, "error");
      }
      setHasLoadedUsers(true);
    } finally {
      if (isManualRefresh) {
        setIsRefreshing(false);
      } else {
        setIsLoading(false);
      }
    }
  }, [language, page, pageSize, searchQuery, showUserMessage, statusFilter, t, token]);

  function refreshUsers() {
    void loadUsers({ manual: true });
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsers();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadUsers]);

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleUsers = users;
  const shouldShowUserSkeleton = !hasLoadedUsers || (isLoading && !visibleUsers.length);
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
  const isSelectedUserOnline = activeSessionCount > 0;
  const hasDraftChanges =
    !!selectedUser &&
    !!accessDraft &&
    accessDraft.userId === selectedUser.id &&
    (accessDraft.role !== selectedUser.role || accessDraft.status !== selectedUser.status);

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
        return;
      }

      setAccessDraft({ userId: selectedUser.id, role: selectedUser.role, status: selectedUser.status });
      setDetailSessionPage(1);
      void loadSelectedUserSessions(selectedUser.id);
      void loadSelectedUserLogs(selectedUser);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadSelectedUserLogs, loadSelectedUserSessions, selectedUser]);

  async function updateUserAccess(userId: string, role: Role, status: UserStatus) {
    if (!token) {
      return;
    }

    try {
      await api.updateUserAccess(token, userId, role, status);
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
      temporaryPassword: usesCustomTemporaryPassword ? createUserDraft.temporaryPassword : "",
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
      });
      setUsesCustomTemporaryPassword(false);
      await loadUsers();
      setSelectedUserId(createdUser.id);
      showCreateUserMessage(t("users.userCreated", { username: createdUser.username }), "success");
    } catch (error) {
      showCreateUserMessage(localizeApiError(error, language, t("users.userCreateFailed")), "error");
    } finally {
      setIsCreatingUser(false);
    }
  }

  function requestUserAccessChange(managedUser: UserAdmin, role: Role, status: UserStatus) {
    if (managedUser.role === role && managedUser.status === status) {
      return;
    }

    showUserMessage(null);
    setPendingAccessChange({
      userId: managedUser.id,
      displayName: managedUser.displayName,
      username: managedUser.username,
      fromRole: managedUser.role,
      toRole: role,
      fromStatus: managedUser.status,
      toStatus: status,
    });
  }

  function requestDraftAccessChange() {
    if (!selectedUser || !accessDraft || !hasDraftChanges) {
      return;
    }

    requestUserAccessChange(selectedUser, accessDraft.role, accessDraft.status);
  }

  async function confirmUserAccessChange() {
    if (!pendingAccessChange) {
      return;
    }

    const change = pendingAccessChange;
    setPendingAccessChange(null);
    await updateUserAccess(change.userId, change.toRole, change.toStatus);
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

      <div className="identity-section">
        <div className="filter-toolbar">
          <label className="search-field">
            <Search size={16} />
            <input
              value={searchQuery}
              onChange={(event) => {
                setSearchQuery(event.target.value);
                setPage(1);
              }}
              placeholder={t("users.searchPlaceholder")}
            />
          </label>
          <label className="filter-select-field">
            <Filter size={16} />
            <select
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value as UserStatus | "All");
                setPage(1);
              }}
            >
              <option value="PendingApproval">{t("settings.statusPending")}</option>
              <option value="Active">{t("settings.statusActive")}</option>
              <option value="Rejected">{t("settings.statusRejected")}</option>
              <option value="All">{t("users.statusAll")}</option>
            </select>
          </label>
        </div>
        {message ? <div className={userMessageClassName}>{message}</div> : null}
      </div>

      <div className="management-layout">
        <div className="management-left-column">
        <section className="identity-section">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.listEyebrow")}</span>
              <h3>{t("users.listTitle")}</h3>
            </div>
            <UserCog size={22} />
          </div>
          <div className="user-management-list">
            {shouldShowUserSkeleton ? <UserManagementSkeleton /> : null}
            {visibleUsers.map((managedUser) => (
              <article className="settings-row user-management-row" key={managedUser.id}>
                <div className="stacked-summary">
                  <span className={`status-pill status-${managedUser.status.toLowerCase()}`}>
                    {userStatusLabel(language, managedUser.status)}
                  </span>
                  <strong>{managedUser.displayName}</strong>
                  <small>
                    {managedUser.username} / {managedUser.email}
                  </small>
                </div>
                <button
                  className={`secondary-button context-button ${selectedUserId === managedUser.id ? "is-active" : ""}`}
                  type="button"
                  onClick={() => setSelectedUserId(managedUser.id)}
                >
                  {t("users.viewDetail")}
                </button>
              </article>
            ))}
            {!visibleUsers.length && hasLoadedUsers && !isLoading ? <p className="status-line">{t("users.empty")}</p> : null}
          </div>
          <PaginationControls
            currentPage={currentPage}
            language={language}
            onNext={() => setPage((value) => Math.min(value + 1, totalPages))}
            onPageChange={setPage}
            onPrevious={() => setPage((value) => Math.max(value - 1, 1))}
            totalPages={totalPages}
          />
        </section>
        <section className="identity-section user-create-disclosure">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.createEyebrow")}</span>
              <h3>{t("users.createTitle")}</h3>
            </div>
            <button
              className={isCreateUserOpen ? "secondary-button" : "success-button create-user-toggle-button"}
              type="button"
              onClick={() => {
                setIsCreateUserOpen((isOpen) => !isOpen);
                showCreateUserMessage(null);
              }}
            >
              <UserPlus size={17} />
              {isCreateUserOpen ? t("common.close") : t("users.createUser")}
            </button>
          </div>
          {isCreateUserOpen ? (
            <div className="admin-create-panel">
              <p className="helper-copy">{t("users.createDescription")}</p>
              <div className="admin-create-grid">
                <input
                  value={createUserDraft.username}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, username: event.target.value }))}
                  placeholder={t("login.username")}
                />
                <input
                  value={createUserDraft.displayName}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, displayName: event.target.value }))}
                  placeholder={t("login.displayName")}
                />
                <input
                  value={createUserDraft.email}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, email: event.target.value }))}
                  placeholder={t("login.email")}
                  type="email"
                />
                {usesCustomTemporaryPassword ? (
                  <input
                    value={createUserDraft.temporaryPassword}
                    onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, temporaryPassword: event.target.value }))}
                    placeholder={t("users.temporaryPassword")}
                    type="password"
                  />
                ) : (
                  <div className="generated-password-placeholder">
                    <Sparkles size={16} />
                    <span>{t("users.autoTemporaryPassword")}</span>
                  </div>
                )}
                <select
                  value={createUserDraft.role}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, role: event.target.value as Role }))}
                >
                  <option value="Admin">Admin</option>
                  <option value="User">User</option>
                  <option value="Approver">Approver</option>
                </select>
                <select
                  value={createUserDraft.status}
                  onChange={(event) => setCreateUserDraft((draft) => ({ ...draft, status: event.target.value as UserStatus }))}
                >
                  <option value="PendingApproval">{t("settings.statusPending")}</option>
                  <option value="Active">{t("settings.statusActive")}</option>
                  <option value="Rejected">{t("settings.statusRejected")}</option>
                </select>
                <small className="admin-create-note">
                  {usesCustomTemporaryPassword ? t("users.customTemporaryPasswordMailNote") : t("users.temporaryPasswordMailNote")}
                </small>
              </div>
              <div className="admin-create-actions">
                <label className="checkbox-line custom-password-toggle compact-password-toggle">
                  <input
                    checked={usesCustomTemporaryPassword}
                    onChange={(event) => {
                      setUsesCustomTemporaryPassword(event.target.checked);
                      if (!event.target.checked) {
                        setCreateUserDraft((draft) => ({ ...draft, temporaryPassword: "" }));
                      }
                    }}
                    type="checkbox"
                  />
                  <span>{t("users.useCustomTemporaryPassword")}</span>
                </label>
                <button className="success-button create-user-submit-button" type="button" disabled={isCreatingUser} onClick={createUser}>
                  {isCreatingUser ? t("users.creatingUser") : t("users.createUser")}
                </button>
              </div>
              {createUserMessage ? <div className={createUserMessageClassName}>{createUserMessage}</div> : null}
            </div>
          ) : null}
        </section>
        </div>

        <section className={selectedUser ? "identity-section user-detail-panel detail-expanded" : "identity-section user-detail-panel detail-placeholder"}>
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">{t("users.detailEyebrow")}</span>
              <h3>{selectedUser ? selectedUser.displayName : t("users.noSelection")}</h3>
            </div>
            {selectedUser ? (
              <button className="icon-button" type="button" onClick={() => setSelectedUserId(null)} title={t("common.close")}>
                <X size={18} />
              </button>
            ) : (
              <History size={22} />
            )}
          </div>
          {selectedUser ? (
            <div className="user-detail-content">
              <div className="settings-grid compact-grid">
                <article className="settings-row">
                  <span>{t("session.username")}</span>
                  <strong>{selectedUser.username}</strong>
                </article>
                <article className="settings-row">
                  <span>{t("session.role")}</span>
                  <strong>{roleLabel(language, selectedUser.role)}</strong>
                </article>
                <article className="settings-row">
                  <span>{t("settings.emailStatus")}</span>
                  <strong>{selectedUser.isEmailVerified ? t("settings.verified") : t("settings.notVerified")}</strong>
                </article>
                <article className="settings-row">
                  <span>{t("users.onlineStatus")}</span>
                  <strong>{isSelectedUserOnline ? t("users.online") : t("users.offline")}</strong>
                  <small>{t("users.activeSessionCount", { count: activeSessionCount })}</small>
                </article>
                <article className="settings-row">
                  <span>{t("users.mustChangePassword")}</span>
                  <strong>{selectedUser.mustChangePassword ? t("common.yes") : t("common.no")}</strong>
                </article>
              </div>
              <div className="access-editor">
                <select
                  value={accessDraft?.role ?? selectedUser.role}
                  onChange={(event) =>
                    setAccessDraft({
                      userId: selectedUser.id,
                      role: event.target.value as Role,
                      status: accessDraft?.status ?? selectedUser.status,
                    })
                  }
                >
                  <option value="Admin">Admin</option>
                  <option value="User">User</option>
                  <option value="Approver">Approver</option>
                </select>
                <select
                  value={accessDraft?.status ?? selectedUser.status}
                  onChange={(event) =>
                    setAccessDraft({
                      userId: selectedUser.id,
                      role: accessDraft?.role ?? selectedUser.role,
                      status: event.target.value as UserStatus,
                    })
                  }
                >
                  <option value="PendingApproval">{t("settings.statusPending")}</option>
                  <option value="Active">{t("settings.statusActive")}</option>
                  <option value="Rejected">{t("settings.statusRejected")}</option>
                </select>
                <button
                  className="primary-button"
                  type="button"
                  disabled={!hasDraftChanges}
                  onClick={requestDraftAccessChange}
                >
                  {t("users.applyAccessChange")}
                </button>
              </div>
              <section className="identity-section nested-identity-section">
                <div className="section-toolbar">
                  <div>
                    <span className="eyebrow">{t("users.sessionsEyebrow")}</span>
                    <h3>{t("users.sessionsTitle")}</h3>
                  </div>
                  <ShieldCheck size={22} />
                </div>
                {isLoadingUserSessions ? <p className="status-line">{t("common.loading")}</p> : null}
                <div className="session-list">
                  {visibleSelectedUserSessions.map((session) => (
                    <article className="settings-row session-row" key={session.id}>
                      <div className="stacked-summary">
                        <span>{session.isCurrent ? t("settings.currentSession") : t("settings.otherSession")}</span>
                        <strong>{formatSessionExpiry(session.expiresAt, language)}</strong>
                        <small>
                          {session.lastSeenAt
                            ? t("settings.lastSeen", { value: formatSessionExpiry(session.lastSeenAt, language) })
                            : t("settings.notSeenYet")}
                        </small>
                        <small>{t("settings.createdAt", { value: formatSessionExpiry(session.createdAt, language) })}</small>
                        <small>{t("settings.device", { value: summarizeUserAgent(session.userAgent, language) })}</small>
                        <small>{t("settings.ipAddress", { value: formatIpAddress(session.ipAddress, language) })}</small>
                        <small>{t(session.rememberedDevice ? "settings.rememberedDevice" : "settings.standardSession")}</small>
                      </div>
                      <button
                        className="secondary-button danger-button"
                        type="button"
                        disabled={session.isCurrent && selectedUser.id === activeUser.id}
                        onClick={() => requestSessionRevoke(session)}
                      >
                        {t("settings.revokeSession")}
                      </button>
                    </article>
                  ))}
                  {!selectedUserSessions.length && !isLoadingUserSessions ? (
                    <p className="status-line">{t("users.noActiveSessions")}</p>
                  ) : null}
                </div>
                {selectedUserSessions.length > detailSessionPageSize ? (
                  <PaginationControls
                    currentPage={currentDetailSessionPage}
                    language={language}
                    onNext={() => setDetailSessionPage((value) => Math.min(value + 1, detailSessionTotalPages))}
                    onPageChange={setDetailSessionPage}
                    onPrevious={() => setDetailSessionPage((value) => Math.max(value - 1, 1))}
                    totalPages={detailSessionTotalPages}
                  />
                ) : null}
              </section>
              {isLoadingUserLogs ? <p className="status-line">{t("common.loading")}</p> : null}
              <SystemAuditTimeline logs={selectedUserLogs} language={language} emptyText={t("users.noUserLogs")} />
              <div className="detail-danger-action">
                <button
                  className="danger-button strong-danger-button"
                  type="button"
                  disabled={selectedUser.id === activeUser.id}
                  onClick={() => requestUserDelete(selectedUser)}
                >
                  {t("users.deleteUser")}
                </button>
              </div>
            </div>
          ) : (
            <p className="status-line">{t("users.noSelectionHelp")}</p>
          )}
        </section>
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
      {toast ? <WorkspaceToast kind={toast.kind} text={toast.text} /> : null}
    </section>
  );
}

function UserManagementSkeleton() {
  return (
    <>
      {Array.from({ length: 4 }, (_, index) => (
        <article className="settings-row user-management-row user-management-skeleton" key={index}>
          <div className="stacked-summary">
            <span />
            <strong />
            <small />
          </div>
          <span />
        </article>
      ))}
    </>
  );
}

function waitForMinimumDelay(startedAt: number, minimumDelayMs: number) {
  const remainingMs = minimumDelayMs - (Date.now() - startedAt);
  return remainingMs > 0 ? new Promise((resolve) => window.setTimeout(resolve, remainingMs)) : Promise.resolve();
}
