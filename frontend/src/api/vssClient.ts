import {
  useApiQuery,
  apiMutate,
  apiGetBlob,
} from "@univerus/udp-react-enterprise-component-library";

/**
 * The VSS backend (BFF) base URL. Read straight from env — this is our own app
 * service, not a Unity gateway service (those come from ConfigService.*ApiUrl).
 */
export const VSS_BASE = import.meta.env.REACT_APP_VSS_API_DOMAIN ?? "http://localhost:5047";

// ------------------------------------------------------------------ API types
export interface Me {
  user: { id: string; email: string; displayName: string; firstName?: string; lastName?: string };
  linkState: "Unlinked" | "PendingLink" | "Linked";
  role: string;
  vendorNumber?: string | null;
  vendorName?: string | null;
  profileCompletePct: number;
  pendingChangeCount: number;
}

export interface Address {
  isPoBox: boolean; poBox?: string | null; remitStreet: string; houseNumber?: string | null;
  remitCity: string; remitState: string; remitZip: string; remitCountry: string; physicalAddress?: string | null;
}
export interface Banking {
  paymentMethod: string; bankName?: string | null; routingNumberMasked?: string | null; accountNumberMasked?: string | null; accountType: string;
}
export interface Tax {
  legalTaxName?: string | null; taxIdType: string; tinMasked?: string | null; taxClassification?: string | null; exemptPayee: string; w9OnFile?: string | null;
}
export interface Contacts {
  primaryContact?: string | null; primaryTitle?: string | null; primaryEmail?: string | null; primaryPhone?: string | null;
  apContactName?: string | null; apEmail?: string | null; salesContactName?: string | null; salesEmail?: string | null;
}
export interface VendorDoc { id: string; name: string; fileRef?: string | null; validity: string; status: string; }

export interface Vendor {
  number: string; legalName: string; dba?: string | null; entityType: string; website?: string | null; status: string;
  address: Address; banking: Banking; tax: Tax; contacts: Contacts;
  categoryCodes: string[]; documents: VendorDoc[];
}

export interface LinkMatchResult {
  linkRequestId?: string | null; matched: boolean;
  vendorNumber?: string | null; vendorName?: string | null;
  remitCity?: string | null; remitState?: string | null; remitZip?: string | null; tinMasked?: string | null;
  status: string;
}

export interface ChangeDiff { field: string; fromValue?: string | null; toValue?: string | null; }
export interface ChangeRequest {
  id: string; code: string; vendorName: string; section: string; submittedByName: string; submittedAt: string; status: string; diffs: ChangeDiff[];
  documentId?: string | null; documentName?: string | null;
}

export interface LinkRequestCreate {
  method: "VendorNumberPin" | "TaxIdZip";
  vendorNumber?: string; pin?: string; taxId?: string; zip?: string;
}
export interface ChangeRequestCreate { section: string; diffs: ChangeDiff[]; }
export interface DocumentUpload { name: string; fileName: string; contentType: string; contentBase64: string; }

// ------------------------------------------------------------------ Query keys
export const qk = {
  me: [VSS_BASE, "api/v1/me"],
  vendor: [VSS_BASE, "api/v1/vendor"],
  changeRequests: [VSS_BASE, "api/v1/change-requests?mine=true"],
};

// ------------------------------------------------------------------ Read hooks
export const useMe = () => useApiQuery<Me>(VSS_BASE, "api/v1/me");

export const useVendor = (enabled: boolean) =>
  useApiQuery<Vendor>(VSS_BASE, "api/v1/vendor", undefined, { enabled });

export const useMyChangeRequests = (enabled: boolean) =>
  useApiQuery<ChangeRequest[]>(VSS_BASE, "api/v1/change-requests?mine=true", undefined, { enabled });

// ------------------------------------------------------------------ Mutations
export const linkRequests = {
  create: (body: LinkRequestCreate) => apiMutate<LinkMatchResult>(VSS_BASE, "api/v1/link-requests", { body }),
  confirm: (id: string) => apiMutate<Me>(VSS_BASE, `api/v1/link-requests/${id}/confirm`, { method: "POST" }),
};

export const changeRequests = {
  create: (body: ChangeRequestCreate) => apiMutate<ChangeRequest>(VSS_BASE, "api/v1/change-requests", { body }),
};

export const documents = {
  upload: (body: DocumentUpload) => apiMutate<VendorDoc>(VSS_BASE, "api/v1/documents", { body }),
  /** Authorized fetch of a stored document's bytes (for PDF preview). */
  content: (id: string) => apiGetBlob(VSS_BASE, `api/v1/documents/${id}/content`),
};
