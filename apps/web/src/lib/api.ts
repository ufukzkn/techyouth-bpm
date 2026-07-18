import type {
  CreateFormRequest,
  CreateFormVersionRequest,
  CreateProcessDefinitionRequest,
  CreateProcessDefinitionVersionRequest,
  CreateCommunityRequest,
  CreateCommunityRoleRequest,
  CreateUserAdminRequest,
  AdminPasswordResetRequest,
  AdminPasswordResetResponse,
  BrowserSessionResponse,
  EmailVerificationStartResponse,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  FormDefinition,
  FormDefinitionVersion,
  Community,
  CommunityRole,
  CommunitySummary,
  DashboardSummary,
  NotificationListParams,
  NotificationPage,
  PagedResult,
  ProcessDetail,
  ProcessDefinition,
  ProcessDefinitionSummary,
  ProcessDefinitionVersion,
  RunnableProcessDefinition,
  ProcessSummary,
  ProcessTask,
  ProcessListParams,
  TaskListParams,
  RegisterResponse,
  RoleTemplate,
  ResetPasswordRequest,
  StartProcessRequest,
  SystemAuditCategoryCounts,
  SystemAuditLog,
  TaskActionRequest,
  UpdateProfileRequest,
  UpdateCommunityRequest,
  User,
  UserAdmin,
  UserSession,
  UserStatus,
  ChangePasswordRequest,
  ClaimTaskRequest,
  CreateTeamRequest,
  Team,
  TeamCandidatePage,
  TeamMember,
  TeamMemberPage,
  TeamRosterPage,
  TeamPage,
  UserTeamMembership,
  UpdateTeamRequest,
} from "@/lib/types";
import { COOKIE_SESSION_MARKER } from "@/features/session/sessionPersistence";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5291";

type ApiErrorPayload = {
  errors?: unknown;
  message?: unknown;
  title?: unknown;
  detail?: unknown;
};

let unauthorizedHandler: (() => Promise<boolean>) | null = null;

export function setUnauthorizedHandler(handler: (() => Promise<boolean>) | null) {
  unauthorizedHandler = handler;
}

export class ApiError extends Error {
  constructor(
    public readonly errors: string[],
    public readonly statusCode?: number,
  ) {
    super(errors.join(" "));
  }
}

function getCookieValue(name: string) {
  if (typeof document === "undefined") {
    return "";
  }

  return document.cookie
    .split(";")
    .map((item) => item.trim())
    .find((item) => item.startsWith(`${name}=`))
    ?.split("=")[1] ?? "";
}

function isMutation(method?: string) {
  return ["POST", "PUT", "PATCH", "DELETE"].includes((method ?? "GET").toUpperCase());
}

function normalizeApiErrors(payload: unknown) {
  if (!isRecord(payload)) {
    return ["Request failed."];
  }

  const errors = normalizeErrorsValue(payload.errors);
  if (errors.length > 0) {
    return errors;
  }

  for (const key of ["message", "detail", "title"] as const) {
    const value = payload[key];
    if (typeof value === "string" && value.trim()) {
      return [value.trim()];
    }
  }

  return ["Request failed."];
}

function normalizeErrorsValue(value: unknown) {
  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
  }

  if (!isRecord(value)) {
    return [];
  }

  return Object.values(value).flatMap((item) =>
    Array.isArray(item)
      ? item.filter((message): message is string => typeof message === "string" && message.trim().length > 0)
      : typeof item === "string" && item.trim()
        ? [item.trim()]
        : [],
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

async function request<T>(
  path: string,
  init?: RequestInit & { token?: string },
  hasRetriedAfterRefresh = false,
): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Content-Type", "application/json");

  if (init?.token && init.token !== COOKIE_SESSION_MARKER && !init.token.startsWith("demo-")) {
    headers.set("Authorization", `Bearer ${init.token}`);
  }

  const csrfToken = getCookieValue("techyouth_csrf");
  if (csrfToken && isMutation(init?.method)) {
    headers.set("X-CSRF-Token", decodeURIComponent(csrfToken));
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    credentials: "include",
  });

  if (!response.ok) {
    if (response.status === 401 && init?.token && unauthorizedHandler && !hasRetriedAfterRefresh) {
      const recovered = await unauthorizedHandler();
      if (recovered) {
        return request<T>(path, init, true);
      }
    }

    const payload: ApiErrorPayload = await response.json().catch(() => ({}));
    throw new ApiError(normalizeApiErrors(payload), response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

function normalizePagedResult<T>(value: PagedResult<T> | T[], page = 1, pageSize?: number): PagedResult<T> {
  if (Array.isArray(value)) {
    return {
      items: value,
      page,
      pageSize: pageSize ?? value.length,
      totalCount: value.length,
    };
  }

  return {
    ...value,
    items: Array.isArray(value.items) ? value.items : [],
  };
}

function buildTeamMemberSearch(params: { query?: string; page?: number; pageSize?: number }) {
  const search = new URLSearchParams();
  if (params.query?.trim()) search.set("query", params.query.trim());
  if (params.page) search.set("page", String(params.page));
  if (params.pageSize) search.set("pageSize", String(params.pageSize));
  return search;
}

export const api = {
  register(username: string, displayName: string, email: string, password: string, communityCode: string) {
    return request<RegisterResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ username, displayName, email, password, communityCode }),
    });
  },
  login(username: string, password: string, rememberMe = false) {
    return request<BrowserSessionResponse>("/api/auth/browser-login", {
      method: "POST",
      body: JSON.stringify({ username, password, rememberMe }),
    });
  },
  refreshSession() {
    return request<BrowserSessionResponse>("/api/auth/refresh", { method: "POST" });
  },
  me(token: string) {
    return request<User>("/api/auth/me", { token });
  },
  meFromCookie() {
    return request<User>("/api/auth/me");
  },
  logout(token: string) {
    return request<void>("/api/auth/logout", { method: "POST", token });
  },
  forgotPassword(payload: ForgotPasswordRequest) {
    return request<ForgotPasswordResponse>("/api/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },
  resetPassword(payload: ResetPasswordRequest) {
    return request<void>("/api/auth/reset-password", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },
  updateProfile(token: string, payload: UpdateProfileRequest) {
    return request<User>("/api/auth/me/profile", {
      method: "PATCH",
      token,
      body: JSON.stringify(payload),
    });
  },
  changePassword(token: string, payload: ChangePasswordRequest) {
    return request<User>("/api/auth/me/password", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  listSessions(token: string) {
    return request<UserSession[]>("/api/auth/sessions", { token });
  },
  revokeSession(token: string, sessionId: string) {
    return request<void>(`/api/auth/sessions/${sessionId}`, { method: "DELETE", token });
  },
  startEmailVerification(token: string) {
    return request<EmailVerificationStartResponse>("/api/auth/me/email-verification", { method: "POST", token });
  },
  confirmEmailVerification(token: string, code: string) {
    return request<User>("/api/auth/me/email-verification/confirm", {
      method: "POST",
      token,
      body: JSON.stringify({ code }),
    });
  },
  startPublicEmailVerification(usernameOrEmail: string) {
    return request<EmailVerificationStartResponse>("/api/auth/public-email-verification/start", {
      method: "POST",
      body: JSON.stringify({ usernameOrEmail }),
    });
  },
  confirmPublicEmailVerification(usernameOrEmail: string, code: string) {
    return request<RegisterResponse>("/api/auth/public-email-verification/confirm", {
      method: "POST",
      body: JSON.stringify({ usernameOrEmail, code }),
    });
  },
  listUsers(
    token: string,
    params: { query?: string; status?: UserStatus | "All"; statuses?: UserStatus[]; communityId?: string | null; communityRoleId?: string | null; page?: number; pageSize?: number } = {},
  ) {
    const search = new URLSearchParams();
    if (params.query) {
      search.set("query", params.query);
    }
    if (params.status && params.status !== "All") {
      search.set("status", params.status);
    }
    for (const status of params.statuses ?? []) {
      search.append("statuses", status);
    }
    if (params.communityId) {
      search.set("communityId", params.communityId);
    }
    if (params.communityRoleId) {
      search.set("communityRoleId", params.communityRoleId);
    }
    if (params.page) {
      search.set("page", String(params.page));
    }
    if (params.pageSize) {
      search.set("pageSize", String(params.pageSize));
    }

    return request<PagedResult<UserAdmin> | UserAdmin[]>(`/api/users${search.size ? `?${search}` : ""}`, {
      token,
    }).then((result) => normalizePagedResult(result, params.page, params.pageSize));
  },
  createUser(token: string, payload: CreateUserAdminRequest) {
    return request<UserAdmin>("/api/users", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateUserAccess(
    token: string,
    userId: string,
    status: UserStatus,
    communityId?: string | null,
    communityRoleId?: string | null,
  ) {
    return request<UserAdmin>(`/api/users/${userId}/access`, {
      method: "PATCH",
      token,
      body: JSON.stringify({ role: "User", status, communityId, communityRoleId }),
    });
  },
  listCommunities(token: string) {
    return request<Community[]>("/api/communities", { token });
  },
  createCommunity(token: string, payload: CreateCommunityRequest) {
    return request<Community>("/api/communities", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateCommunity(token: string, communityId: string, payload: UpdateCommunityRequest) {
    return request<Community>(`/api/communities/${communityId}`, {
      method: "PATCH",
      token,
      body: JSON.stringify(payload),
    });
  },
  regenerateCommunityInviteCode(token: string, communityId: string) {
    return request<Community>(`/api/communities/${communityId}/invite-code/regenerate`, {
      method: "PATCH",
      token,
    });
  },
  getCommunitySummary(token: string, communityId: string) {
    return request<CommunitySummary>(`/api/communities/${communityId}/summary`, { token });
  },
  listRoleTemplates(token: string) {
    return request<RoleTemplate[]>("/api/communities/role-templates", { token });
  },
  listCommunityRoles(token: string, communityId: string) {
    return request<CommunityRole[]>(`/api/communities/${communityId}/roles`, { token });
  },
  createCommunityRole(token: string, communityId: string, payload: CreateCommunityRoleRequest) {
    return request<CommunityRole>(`/api/communities/${communityId}/roles`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateCommunityRole(token: string, communityId: string, roleId: string, payload: CreateCommunityRoleRequest) {
    return request<CommunityRole>(`/api/communities/${communityId}/roles/${roleId}`, {
      method: "PATCH",
      token,
      body: JSON.stringify({
        name: payload.name,
        description: payload.description,
        permissions: payload.permissions,
      }),
    });
  },
  deleteCommunityRole(token: string, communityId: string, roleId: string, replacementRoleId: string) {
    return request<void>(`/api/communities/${communityId}/roles/${roleId}`, {
      method: "DELETE",
      token,
      body: JSON.stringify({ replacementRoleId }),
    });
  },
  listTeams(
    token: string,
    params: { communityId?: string | null; query?: string; isActive?: boolean | null; page?: number; pageSize?: number } = {},
  ) {
    const search = new URLSearchParams();
    if (params.communityId) search.set("communityId", params.communityId);
    if (params.query?.trim()) search.set("query", params.query.trim());
    if (params.isActive !== null && params.isActive !== undefined) search.set("isActive", String(params.isActive));
    if (params.page) search.set("page", String(params.page));
    if (params.pageSize) search.set("pageSize", String(params.pageSize));
    return request<TeamPage>(`/api/teams${search.size ? `?${search}` : ""}`, { token });
  },
  getTeam(token: string, teamId: string) {
    return request<Team>(`/api/teams/${teamId}`, { token });
  },
  createTeam(token: string, payload: CreateTeamRequest) {
    return request<Team>("/api/teams", { method: "POST", token, body: JSON.stringify(payload) });
  },
  updateTeam(token: string, teamId: string, payload: UpdateTeamRequest) {
    return request<Team>(`/api/teams/${teamId}`, { method: "PATCH", token, body: JSON.stringify(payload) });
  },
  listTeamMembers(token: string, teamId: string, params: { query?: string; page?: number; pageSize?: number } = {}) {
    const search = buildTeamMemberSearch(params);
    return request<TeamMemberPage>(`/api/teams/${teamId}/members${search.size ? `?${search}` : ""}`, { token });
  },
  listTeamRoster(token: string, teamId: string, params: { query?: string; page?: number; pageSize?: number } = {}) {
    const search = buildTeamMemberSearch(params);
    return request<TeamRosterPage>(`/api/teams/${teamId}/roster${search.size ? `?${search}` : ""}`, { token });
  },
  listUserTeamMemberships(token: string, userId: string) {
    return request<UserTeamMembership[]>(`/api/users/${userId}/team-memberships`, { token });
  },
  listTeamCandidates(token: string, teamId: string, params: { query?: string; page?: number; pageSize?: number } = {}) {
    const search = buildTeamMemberSearch(params);
    return request<TeamCandidatePage>(`/api/teams/${teamId}/candidates${search.size ? `?${search}` : ""}`, { token });
  },
  listUnassignedTeamMembers(
    token: string,
    params: { communityId: string; query?: string; page?: number; pageSize?: number },
  ) {
    const search = buildTeamMemberSearch(params);
    search.set("communityId", params.communityId);
    return request<TeamCandidatePage>(`/api/teams/unassigned/members?${search}`, { token });
  },
  addTeamMember(token: string, teamId: string, userId: string, isLead = false) {
    return request<TeamMember>(`/api/teams/${teamId}/members`, {
      method: "POST",
      token,
      body: JSON.stringify({ userId, isLead }),
    });
  },
  updateTeamMember(token: string, teamId: string, userId: string, isLead: boolean) {
    return request<TeamMember>(`/api/teams/${teamId}/members/${userId}`, {
      method: "PATCH",
      token,
      body: JSON.stringify({ isLead }),
    });
  },
  removeTeamMember(token: string, teamId: string, userId: string) {
    return request<void>(`/api/teams/${teamId}/members/${userId}`, { method: "DELETE", token });
  },
  deleteUser(token: string, userId: string) {
    return request<void>(`/api/users/${userId}`, { method: "DELETE", token });
  },
  listUserSessions(token: string, userId: string) {
    return request<UserSession[]>(`/api/users/${userId}/sessions`, { token });
  },
  revokeUserSession(token: string, userId: string, sessionId: string) {
    return request<void>(`/api/users/${userId}/sessions/${sessionId}`, { method: "DELETE", token });
  },
  resetUserPasswordByAdmin(token: string, userId: string, payload: AdminPasswordResetRequest) {
    return request<AdminPasswordResetResponse>(`/api/users/${userId}/password-reset-by-admin`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  listNotifications(token: string, params: NotificationListParams = {}) {
    const search = new URLSearchParams();
    if (params.page) search.set("page", String(params.page));
    if (params.pageSize) search.set("pageSize", String(params.pageSize));
    if (params.query?.trim()) search.set("query", params.query.trim());
    if (params.readStatus && params.readStatus !== "all") search.set("readStatus", params.readStatus);
    if (params.category && params.category !== "all") search.set("category", params.category);
    return request<NotificationPage>(`/api/notifications${search.size ? `?${search}` : ""}`, { token });
  },
  markNotificationRead(token: string, notificationId: string) {
    return request<void>(`/api/notifications/${notificationId}/read`, { method: "PATCH", token });
  },
  setNotificationReadState(token: string, notificationId: string, isRead: boolean) {
    return request<void>(`/api/notifications/${notificationId}/read-state`, {
      method: "PATCH",
      token,
      body: JSON.stringify({ isRead }),
    });
  },
  markAllNotificationsRead(token: string) {
    return request<void>("/api/notifications/read-all", { method: "POST", token });
  },
  listSystemAuditLogs(
    token: string,
    params: { query?: string; category?: string; page?: number; pageSize?: number; sortBy?: "createdAt" | "action" | "actor"; sortDirection?: "asc" | "desc" } = {},
  ) {
    const search = new URLSearchParams();
    if (params.query) {
      search.set("query", params.query);
    }
    if (params.category && params.category !== "all") {
      search.set("category", params.category);
    }
    if (params.page) {
      search.set("page", String(params.page));
    }
    if (params.pageSize) {
      search.set("pageSize", String(params.pageSize));
    }
    if (params.sortBy) {
      search.set("sortBy", params.sortBy);
    }
    if (params.sortDirection) {
      search.set("sortDirection", params.sortDirection);
    }

    return request<PagedResult<SystemAuditLog> | SystemAuditLog[]>(
      `/api/audit/system${search.size ? `?${search}` : ""}`,
      { token },
    ).then((result) => normalizePagedResult(result, params.page, params.pageSize));
  },
  listSystemAuditCounts(token: string, query = "") {
    const search = new URLSearchParams();
    if (query.trim()) {
      search.set("query", query.trim());
    }

    return request<SystemAuditCategoryCounts>(`/api/audit/system/counts${search.size ? `?${search}` : ""}`, { token });
  },
  getDashboardSummary(token: string, scope: "personal" | "community" | "global" = "personal") {
    const search = new URLSearchParams({ scope });
    return request<DashboardSummary>(`/api/dashboard/summary?${search}`, { token });
  },
  listForms(token: string) {
    return request<FormDefinition[]>("/api/forms", { token });
  },
  createForm(token: string, payload: CreateFormRequest) {
    return request<FormDefinition>("/api/forms", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateForm(token: string, id: string, payload: CreateFormRequest) {
    return request<FormDefinition>(`/api/forms/${id}`, {
      method: "PUT",
      token,
      body: JSON.stringify(payload),
    });
  },
  getForm(token: string, id: string) {
    return request<FormDefinition>(`/api/forms/${id}`, { token });
  },
  listFormVersions(token: string, formId: string) {
    return request<FormDefinitionVersion[]>(`/api/forms/${formId}/versions`, { token });
  },
  getFormVersion(token: string, formId: string, versionId: string) {
    return request<FormDefinitionVersion>(`/api/forms/${formId}/versions/${versionId}`, { token });
  },
  createFormVersion(token: string, formId: string, payload: CreateFormVersionRequest) {
    return request<FormDefinitionVersion>(`/api/forms/${formId}/versions`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateFormVersion(token: string, formId: string, versionId: string, payload: CreateFormVersionRequest) {
    return request<FormDefinitionVersion>(`/api/forms/${formId}/versions/${versionId}`, {
      method: "PUT",
      token,
      body: JSON.stringify(payload),
    });
  },
  publishFormVersion(token: string, formId: string, versionId: string) {
    return request<FormDefinitionVersion>(`/api/forms/${formId}/versions/${versionId}/publish`, {
      method: "POST",
      token,
    });
  },
  archiveFormVersion(token: string, formId: string, versionId: string) {
    return request<FormDefinitionVersion>(`/api/forms/${formId}/versions/${versionId}/archive`, {
      method: "POST",
      token,
    });
  },
  listProcessDefinitions(token: string) {
    return request<ProcessDefinitionSummary[]>("/api/process-definitions", { token });
  },
  listRunnableProcessDefinitions(token: string) {
    return request<RunnableProcessDefinition[]>("/api/process-definitions/runnable", { token });
  },
  getProcessDefinition(token: string, id: string) {
    return request<ProcessDefinition>(`/api/process-definitions/${id}`, { token });
  },
  createProcessDefinition(token: string, payload: CreateProcessDefinitionRequest) {
    return request<ProcessDefinition>("/api/process-definitions", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateProcessDefinition(token: string, id: string, payload: Pick<CreateProcessDefinitionRequest, "name" | "description">) {
    return request<ProcessDefinition>(`/api/process-definitions/${id}`, {
      method: "PUT",
      token,
      body: JSON.stringify(payload),
    });
  },
  createProcessDefinitionVersion(token: string, id: string, payload: CreateProcessDefinitionVersionRequest) {
    return request<ProcessDefinitionVersion>(`/api/process-definitions/${id}/versions`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  updateProcessDefinitionVersion(token: string, id: string, versionId: string, payload: CreateProcessDefinitionVersionRequest) {
    return request<ProcessDefinitionVersion>(`/api/process-definitions/${id}/versions/${versionId}`, {
      method: "PUT",
      token,
      body: JSON.stringify(payload),
    });
  },
  publishProcessDefinitionVersion(token: string, id: string, versionId: string) {
    return request<ProcessDefinitionVersion>(`/api/process-definitions/${id}/versions/${versionId}/publish`, {
      method: "POST",
      token,
    });
  },
  startProcess(token: string, payload: StartProcessRequest) {
    return request<ProcessDetail>("/api/processes/start", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  listProcesses(token: string, params: ProcessListParams = {}) {
    const search = new URLSearchParams();
    if (params.page) search.set("page", String(params.page));
    if (params.pageSize) search.set("pageSize", String(params.pageSize));
    if (params.status && params.status !== "all") search.set("status", params.status);
    if (params.scope) search.set("scope", params.scope);
    if (params.sortBy) search.set("sortBy", params.sortBy);
    if (params.sortDirection) search.set("sortDirection", params.sortDirection);
    return request<PagedResult<ProcessSummary> | ProcessSummary[]>(
      `/api/processes${search.size ? `?${search}` : ""}`,
      { token },
    ).then((result) => normalizePagedResult(result, params.page, params.pageSize));
  },
  getProcess(token: string, id: string) {
    return request<ProcessDetail>(`/api/processes/${id}`, { token });
  },
  listMyTasks(token: string, params: TaskListParams = {}) {
    const search = new URLSearchParams();
    if (params.page) search.set("page", String(params.page));
    if (params.pageSize) search.set("pageSize", String(params.pageSize));
    if (params.priority && params.priority !== "all") search.set("priority", params.priority);
    if (params.taskId) search.set("taskId", params.taskId);
    if (params.sortBy) search.set("sortBy", params.sortBy);
    if (params.sortDirection) search.set("sortDirection", params.sortDirection);
    return request<PagedResult<ProcessTask> | ProcessTask[]>(
      `/api/tasks/my${search.size ? `?${search}` : ""}`,
      { token },
    ).then((result) => normalizePagedResult(result, params.page, params.pageSize));
  },
  startProcessVersion(token: string, processDefinitionVersionId: string, formData: Record<string, unknown>) {
    return request<ProcessDetail>("/api/processes/start/version", {
      method: "POST",
      token,
      body: JSON.stringify({ processDefinitionVersionId, formData }),
    });
  },
  claimTask(token: string, taskId: string, payload: ClaimTaskRequest = {}) {
    return request<ProcessTask>(`/api/tasks/${taskId}/claim`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  releaseTask(token: string, taskId: string, payload: ClaimTaskRequest = {}) {
    return request<ProcessTask>(`/api/tasks/${taskId}/claim`, {
      method: "DELETE",
      token,
      body: JSON.stringify(payload),
    });
  },
  executeTaskAction(token: string, taskId: string, payload: TaskActionRequest) {
    return request<ProcessDetail>(`/api/tasks/${taskId}/actions`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
};
