/**
 * DEV STUB of @univerus/udp-react-enterprise-component-library.
 *
 * Re-implements ONLY the exports this app uses, matching the names/signatures
 * documented in Univerus Hub (ConfigService, useApiQuery/useApiMutation,
 * apiGet/apiMutate, RoleIdEnums). On the Univerus network this whole folder is
 * bypassed by removing the alias in vite.config.ts / tsconfig.json and installing
 * the real package — app code does not change.
 *
 * Auth: the real library injects the MSAL bearer token via an Axios interceptor.
 * Here, the app registers a header provider via `setApiAuthProvider` (dev headers
 * or a real bearer token), which every request helper applies.
 */
import { useQuery, useMutation, type UseQueryOptions } from "@tanstack/react-query";

// ---------------------------------------------------------------- ConfigService
export interface UdpConfig {
  UNITY_API_DOMAIN: string;
  UNITY_URL: string;
  UNITY_TENANT_ID: string;
  UNITY_PRODUCT_ID: string;
  UNITY_VERTICAL_ID: string;
}

const DEFAULTS: UdpConfig = {
  UNITY_API_DOMAIN: "https://gateway.unitydev.ca",
  UNITY_URL: "https://unitydev.ca",
  UNITY_TENANT_ID: "",
  UNITY_PRODUCT_ID: "",
  UNITY_VERTICAL_ID: "",
};

let _config: UdpConfig | null = null;
let _markReady!: () => void;
const _ready = new Promise<void>((resolve) => (_markReady = resolve));

export const ConfigService = {
  loadConfigObject(env: Partial<UdpConfig>) {
    _config = { ...DEFAULTS, ...env };
    _markReady();
  },
  waitForConfig() {
    return _ready;
  },
  get config(): UdpConfig {
    if (!_config) throw new Error("ConfigService not initialised — call loadConfigObject() first.");
    return _config;
  },
  get securityV1ApiUrl() { return `${this.config.UNITY_API_DOMAIN}/SecurityService/api/v1`; },
  get securityV2ApiUrl() { return `${this.config.UNITY_API_DOMAIN}/SecurityService/api/v2`; },
  get integrationV1ApiUrl() { return `${this.config.UNITY_API_DOMAIN}/IntegrationService/api/v1`; },
  get integrationV2ApiUrl() { return `${this.config.UNITY_API_DOMAIN}/IntegrationService/api/v2`; },
  get tenantV1ApiUrl() { return `${this.config.UNITY_API_DOMAIN}/TenantService/api/v1`; },
};

// ---------------------------------------------------------------- Auth plumbing
type HeaderProvider = () => Promise<Record<string, string>> | Record<string, string>;
let _authHeaders: HeaderProvider = () => ({});

/** Registered by the app's authProvider (dev headers or a real bearer token). */
export function setApiAuthProvider(fn: HeaderProvider) {
  _authHeaders = fn;
}

// ---------------------------------------------------------------- Request core
function join(baseURL: string, url: string) {
  return `${baseURL.replace(/\/+$/, "")}/${url.replace(/^\/+/, "")}`;
}

async function request<T>(baseURL: string, url: string, method: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { ...(await _authHeaders()) };
  if (body !== undefined) headers["Content-Type"] = "application/json";

  const res = await fetch(join(baseURL, url), {
    method,
    headers,
    credentials: "include",
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${method} ${url} → ${res.status} ${res.statusText}${text ? `: ${text}` : ""}`);
  }
  if (res.status === 204) return undefined as T;
  const ct = res.headers.get("content-type") ?? "";
  return (ct.includes("application/json") ? await res.json() : await res.text()) as T;
}

export function apiGet<T>(baseURL: string, url: string): Promise<T> {
  return request<T>(baseURL, url, "GET");
}

/** Authorized GET that returns the raw response as a Blob (e.g. a PDF for preview). */
export async function apiGetBlob(baseURL: string, url: string): Promise<Blob> {
  const headers: Record<string, string> = { ...(await _authHeaders()) };
  const res = await fetch(join(baseURL, url), { method: "GET", headers, credentials: "include" });
  if (!res.ok) throw new Error(`GET ${url} → ${res.status} ${res.statusText}`);
  return res.blob();
}
export function apiMutate<T>(baseURL: string, url: string, opts?: { method?: string; body?: unknown }): Promise<T> {
  return request<T>(baseURL, url, opts?.method ?? "POST", opts?.body);
}

// ---------------------------------------------------------------- React hooks
export function useApiQuery<T>(
  baseURL: string,
  url: string,
  _config?: Record<string, unknown>,
  options?: Omit<UseQueryOptions<T, Error>, "queryKey" | "queryFn">,
) {
  return useQuery<T, Error>({
    queryKey: [baseURL, url],
    queryFn: () => apiGet<T>(baseURL, url),
    ...options,
  });
}

export function useApiMutation<TBody = unknown, TResp = unknown>(
  baseURL: string,
  url: string,
  opts?: { method?: string },
) {
  return useMutation<TResp, Error, TBody>({
    mutationFn: (body: TBody) => apiMutate<TResp>(baseURL, url, { method: opts?.method ?? "POST", body }),
  });
}

// ---------------------------------------------------------------- Roles
export const RoleIdEnums = {
  Unity_System_Administrator: 1,
  Vendor: 100,
  CityStaff: 101,
} as const;
export type RoleId = (typeof RoleIdEnums)[keyof typeof RoleIdEnums];
