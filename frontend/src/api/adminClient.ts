import { useApiQuery, apiMutate } from "@univerus/udp-react-enterprise-component-library";
import { VSS_BASE, type ChangeRequest } from "./vssClient";

// ------------------------------------------------------------------ Admin types
export interface AdminStats {
  erpStatus: string;
  pendingLinks: number;
  pendingChanges: number;
  linkedVendors: number;
}
export interface AdminVendor {
  number: string;
  name: string;
  category: string;
  lastSync?: string | null;
  status: string;
}
export interface AdminLinkRequest {
  id: string;
  company: string;
  email: string;
  method: string;
  matchedVendorNumber?: string | null;
  createdAt: string;
  status: string;
}

// ------------------------------------------------------------------ Query keys
export const adminQk = {
  stats: [VSS_BASE, "api/v1/admin/stats"],
  changeRequests: [VSS_BASE, "api/v1/admin/change-requests"],
  linkRequests: [VSS_BASE, "api/v1/admin/link-requests"],
  vendors: [VSS_BASE, "api/v1/admin/vendors"],
  change: (id: string) => [VSS_BASE, `api/v1/admin/change-requests/${id}`],
};

// ------------------------------------------------------------------ Read hooks
export const useAdminStats = () => useApiQuery<AdminStats>(VSS_BASE, "api/v1/admin/stats");
export const useAdminChangeRequests = () => useApiQuery<ChangeRequest[]>(VSS_BASE, "api/v1/admin/change-requests");
export const useAdminChangeRequest = (id: string) => useApiQuery<ChangeRequest>(VSS_BASE, `api/v1/admin/change-requests/${id}`);
export const useAdminVendors = () => useApiQuery<AdminVendor[]>(VSS_BASE, "api/v1/admin/vendors");
export const useAdminLinkRequests = () => useApiQuery<AdminLinkRequest[]>(VSS_BASE, "api/v1/admin/link-requests");

export interface ErpTestResult { provider: string; ok: boolean; latencyMs: number; message: string; }

// ------------------------------------------------------------------ Mutations
export const adminApi = {
  testErp: () => apiMutate<ErpTestResult>(VSS_BASE, "api/v1/admin/erp/test", { method: "POST" }),
  approveChange: (id: string, note?: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/change-requests/${id}/approve`, { method: "POST", body: { note } }),
  rejectChange: (id: string, note?: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/change-requests/${id}/reject`, { method: "POST", body: { note } }),
  approveLink: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/link-requests/${id}/approve`, { method: "POST" }),
  rejectLink: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/link-requests/${id}/reject`, { method: "POST" }),
};
