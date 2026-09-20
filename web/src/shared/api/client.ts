export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function resolveBaseUrl(): string {
  return typeof window === "undefined" ? "" : window.location.origin;
}

function validationMessages(body: Record<string, unknown>): string[] {
  const errors = body.errors;
  if (errors === null || typeof errors !== "object" || Array.isArray(errors)) {
    return [];
  }

  return Object.entries(errors).flatMap(([field, values]) =>
    Array.isArray(values)
      ? values
          .filter((value): value is string => typeof value === "string")
          .map((value) => (field ? `${field}: ${value}` : value))
      : [],
  );
}

async function readError(response: Response): Promise<ApiError> {
  const body: unknown = await response.json().catch(() => null);
  if (body !== null && typeof body === "object" && !Array.isArray(body)) {
    const record = body as Record<string, unknown>;
    if (typeof record.detail === "string") {
      return new ApiError(response.status, record.detail);
    }
    const messages = validationMessages(record);
    if (messages.length > 0) {
      return new ApiError(response.status, messages.join("; "));
    }
  }
  return new ApiError(response.status, response.statusText || `Request failed (${response.status})`);
}

async function request<T>(path: string, init: RequestInit): Promise<T> {
  const response = await fetch(`${resolveBaseUrl()}/api${path}`, {
    ...init,
    headers: {
      Accept: "application/json",
      ...(init.body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...init.headers,
    },
  });

  if (!response.ok) {
    throw await readError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return text.length === 0 ? (undefined as T) : (JSON.parse(text) as T);
}

export function apiGet<T>(path: string): Promise<T> {
  return request<T>(path, { method: "GET" });
}

export function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });
}

export function apiPut<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) });
}

export function apiDelete<T = void>(path: string): Promise<T> {
  return request<T>(path, { method: "DELETE" });
}
