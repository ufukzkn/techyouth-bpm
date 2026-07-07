import type {
  CreateFormRequest,
  CreateUserAdminRequest,
  EmailVerificationStartResponse,
  FormDefinition,
  LoginResponse,
  PagedResult,
  ProcessDetail,
  ProcessSummary,
  ProcessTask,
  RegisterResponse,
  StartProcessRequest,
  SystemAuditCategoryCounts,
  SystemAuditLog,
  TaskActionRequest,
  UpdateProfileRequest,
  User,
  UserAdmin,
  UserSession,
  UserStatus,
  Role,
  ChangePasswordRequest,
} from "@/lib/types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5291";

type ApiErrorPayload = {
  errors?: string[];
};

export class ApiError extends Error {
  constructor(
    public readonly errors: string[],
    public readonly statusCode?: number,
  ) {
    super(errors.join(" "));
  }
}

async function request<T>(path: string, init?: RequestInit & { token?: string }): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Content-Type", "application/json");

  if (init?.token) {
    headers.set("Authorization", `Bearer ${init.token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (!response.ok) {
    const payload = (await response.json().catch(() => ({}))) as ApiErrorPayload;
    throw new ApiError(payload.errors ?? ["Request failed."], response.status);
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

export const api = {
  register(username: string, displayName: string, email: string, password: string) {
    return request<RegisterResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ username, displayName, email, password }),
    });
  },
  login(username: string, password: string, rememberMe = false) {
    return request<LoginResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password, rememberMe }),
    });
  },
  me(token: string) {
    return request<User>("/api/auth/me", { token });
  },
  logout(token: string) {
    return request<void>("/api/auth/logout", { method: "POST", token });
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
  listUsers(
    token: string,
    params: { query?: string; status?: UserStatus | "All"; page?: number; pageSize?: number } = {},
  ) {
    const search = new URLSearchParams();
    if (params.query) {
      search.set("query", params.query);
    }
    if (params.status && params.status !== "All") {
      search.set("status", params.status);
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
  updateUserAccess(token: string, userId: string, role: Role, status: UserStatus) {
    return request<UserAdmin>(`/api/users/${userId}/access`, {
      method: "PATCH",
      token,
      body: JSON.stringify({ role, status }),
    });
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
  listSystemAuditLogs(
    token: string,
    params: { query?: string; category?: string; page?: number; pageSize?: number } = {},
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
  startProcess(token: string, payload: StartProcessRequest) {
    return request<ProcessDetail>("/api/processes/start", {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
  listProcesses(token: string) {
    return request<ProcessSummary[]>("/api/processes", { token });
  },
  getProcess(token: string, id: string) {
    return request<ProcessDetail>(`/api/processes/${id}`, { token });
  },
  listMyTasks(token: string) {
    return request<ProcessTask[]>("/api/tasks/my", { token });
  },
  executeTaskAction(token: string, taskId: string, payload: TaskActionRequest) {
    return request<ProcessDetail>(`/api/tasks/${taskId}/actions`, {
      method: "POST",
      token,
      body: JSON.stringify(payload),
    });
  },
};
