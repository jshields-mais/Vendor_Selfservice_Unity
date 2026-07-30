import { useApiQuery, apiMutate } from "@univerus/udp-react-enterprise-component-library";
import { VSS_BASE, type ChangeRequest, type DocumentType, type ContactCode } from "./vssClient";

export interface DocumentTypeUpsert { code: string; description: string; isActive: boolean; sortOrder: number; }
export interface ContactCodeUpsert { category: string; code: string; description: string; isActive: boolean; sortOrder: number; }

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
  documentTypes: [VSS_BASE, "api/v1/admin/document-types"],
  contactCodes: [VSS_BASE, "api/v1/admin/contact-codes"],
  erpConfig: [VSS_BASE, "api/v1/admin/erp/config"],
};

// ------------------------------------------------------------------ Read hooks
export const useAdminStats = () => useApiQuery<AdminStats>(VSS_BASE, "api/v1/admin/stats");
export const useAdminChangeRequests = () => useApiQuery<ChangeRequest[]>(VSS_BASE, "api/v1/admin/change-requests");
export const useAdminChangeRequest = (id: string) => useApiQuery<ChangeRequest>(VSS_BASE, `api/v1/admin/change-requests/${id}`);
export const useAdminVendors = () => useApiQuery<AdminVendor[]>(VSS_BASE, "api/v1/admin/vendors");
export const useAdminLinkRequests = () => useApiQuery<AdminLinkRequest[]>(VSS_BASE, "api/v1/admin/link-requests");
export const useAdminDocumentTypes = () => useApiQuery<DocumentType[]>(VSS_BASE, "api/v1/admin/document-types");
export const useAdminContactCodes = () => useApiQuery<ContactCode[]>(VSS_BASE, "api/v1/admin/contact-codes");

export interface ErpTestResult { provider: string; ok: boolean; latencyMs: number; message: string; }

export interface ErpConfig {
  provider: string; authMode: string; secretConfigured: boolean;
  baseUrl: string; principalId: string; querySupplierPath: string; manageSupplierPath: string;
  sampleId: string; tenantId: string; scope: string; companyId: string; updatedAt?: string | null;
}
export type ErpConfigUpdate = Pick<ErpConfig,
  "baseUrl" | "principalId" | "querySupplierPath" | "manageSupplierPath" | "sampleId" | "tenantId" | "scope" | "companyId">;

export const useErpConfig = () => useApiQuery<ErpConfig>(VSS_BASE, "api/v1/admin/erp/config");

// ------------------------------------------------------------------ Mutations
export const adminApi = {
  testErp: () => apiMutate<ErpTestResult>(VSS_BASE, "api/v1/admin/erp/test", { method: "POST" }),
  saveErpConfig: (body: ErpConfigUpdate) => apiMutate<ErpConfig>(VSS_BASE, "api/v1/admin/erp/config", { method: "PUT", body }),
  approveChange: (id: string, note?: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/change-requests/${id}/approve`, { method: "POST", body: { note } }),
  rejectChange: (id: string, note?: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/change-requests/${id}/reject`, { method: "POST", body: { note } }),
  approveLink: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/link-requests/${id}/approve`, { method: "POST" }),
  rejectLink: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/link-requests/${id}/reject`, { method: "POST" }),
  createDocType: (body: DocumentTypeUpsert) =>
    apiMutate<DocumentType>(VSS_BASE, "api/v1/admin/document-types", { method: "POST", body }),
  updateDocType: (id: string, body: DocumentTypeUpsert) =>
    apiMutate<DocumentType>(VSS_BASE, `api/v1/admin/document-types/${id}`, { method: "PUT", body }),
  deleteDocType: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/document-types/${id}`, { method: "DELETE" }),
  createContactCode: (body: ContactCodeUpsert) =>
    apiMutate<ContactCode>(VSS_BASE, "api/v1/admin/contact-codes", { method: "POST", body }),
  updateContactCode: (id: string, body: ContactCodeUpsert) =>
    apiMutate<ContactCode>(VSS_BASE, `api/v1/admin/contact-codes/${id}`, { method: "PUT", body }),
  deleteContactCode: (id: string) =>
    apiMutate<void>(VSS_BASE, `api/v1/admin/contact-codes/${id}`, { method: "DELETE" }),
};
