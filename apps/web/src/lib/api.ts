import type {
  CreateFormRequest,
  FormDefinition,
  LoginResponse,
  ProcessDetail,
  ProcessSummary,
  ProcessTask,
  StartProcessRequest,
  TaskActionRequest,
  User,
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

  return response.json() as Promise<T>;
}

export const api = {
  login(username: string, password: string) {
    return request<LoginResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });
  },
  me(token: string) {
    return request<User>("/api/auth/me", { token });
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
