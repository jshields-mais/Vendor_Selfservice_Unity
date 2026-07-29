namespace Vss.Api.Contracts;

// ---- Identity / session ----
public record UserDto(Guid Id, string Email, string DisplayName, string? FirstName, string? LastName);

public record MeDto(
    UserDto User,
    string LinkState,
    string Role,
    string? VendorNumber,
    string? VendorName,
    int ProfileCompletePct,
    int PendingChangeCount);

// ---- Vendor record (sectioned; secrets masked) ----
public record AddressDto(bool IsPoBox, string? PoBox, string RemitStreet, string? HouseNumber, string RemitCity, string RemitState, string RemitZip, string RemitCountry, string? PhysicalAddress);
public record BankingDto(string PaymentMethod, string? BankName, string? RoutingNumberMasked, string? AccountNumberMasked, string AccountType);
public record TaxDto(string? LegalTaxName, string TaxIdType, string? TinMasked, string? TaxClassification, string ExemptPayee, string? W9OnFile);
public record ContactsDto(string? FirstName, string? LastName, string? Title, string? Function, string? Department, string? Email, string? Phone, string? Mobile, string? Fax);
public record DocumentDto(Guid Id, string Name, string? FileRef, string Validity, string Status, string? TypeCode);
public record CommunicationPreferenceDto(string BusinessDocument, string Channel, string? Email);
public record CommunicationCatalogDocDto(string Name, bool ErpEnabled);
public record CommunicationCatalogDto(CommunicationCatalogDocDto[] Documents, string[] Channels);

public record VendorDto(
    string Number,
    string LegalName,
    string? Dba,
    string EntityType,
    string? Website,
    string Status,
    AddressDto Address,
    BankingDto Banking,
    TaxDto Tax,
    ContactsDto Contacts,
    string[] CategoryCodes,
    DocumentDto[] Documents,
    CommunicationPreferenceDto[] CommunicationPreferences);

// ---- Account linking ----
public record LinkRequestCreateDto(string Method, string? VendorNumber, string? Pin, string? TaxId, string? Zip);
public record LinkMatchResultDto(Guid? LinkRequestId, bool Matched, string? VendorNumber, string? VendorName, string? RemitCity, string? RemitState, string? RemitZip, string? TinMasked, string Status);

// ---- Change requests ----
public record ChangeDiffDto(string Field, string? FromValue, string? ToValue);
public record ChangeRequestCreateDto(string Section, ChangeDiffDto[] Diffs);
public record ChangeRequestDto(Guid Id, string Code, string VendorName, string Section, string SubmittedByName, DateTimeOffset SubmittedAt, string Status, ChangeDiffDto[] Diffs, Guid? DocumentId = null, string? DocumentName = null);
public record ReviewDecisionDto(string? Note);

// ---- Documents ----
/// <summary>A document uploaded from the portal: the selected document type
/// (<paramref name="TypeCode"/>), the original file name, its MIME type, and the file
/// bytes as base64.</summary>
public record DocumentUploadDto(string TypeCode, string FileName, string ContentType, string ContentBase64);

// ---- Document types (configurable) ----
public record DocumentTypeDto(Guid Id, string Code, string Description, bool IsActive, int SortOrder);
public record DocumentTypeUpsertDto(string Code, string Description, bool IsActive, int SortOrder);

// ---- Admin ----
public record AdminLinkRequestDto(Guid Id, string Company, string Email, string Method, string? MatchedVendorNumber, DateTimeOffset CreatedAt, string Status);
public record AdminVendorDto(string Number, string Name, string Category, DateTimeOffset? LastSync, string Status);
public record AdminStatsDto(string ErpStatus, int PendingLinks, int PendingChanges, int LinkedVendors);

// ---- ERP configuration (editable connection settings; secrets never returned) ----
public record ErpConfigDto(
    string Provider,          // running provider, read-only (chosen at startup)
    string AuthMode,          // e.g. "HTTP Basic" / "OAuth 2.0 (client credentials)"
    bool SecretConfigured,    // whether the active provider's secret is set (value never sent)
    string BaseUrl,
    string PrincipalId,       // SAP username / BC client id
    string QuerySupplierPath, // SAP only
    string ManageSupplierPath,// SAP only
    string SampleId,          // SAP supplier id / BC vendor number
    string TenantId,          // BC only
    string Scope,             // BC only
    string CompanyId,         // BC only
    DateTimeOffset? UpdatedAt);

public record ErpConfigUpdateDto(
    string BaseUrl, string PrincipalId, string QuerySupplierPath, string ManageSupplierPath,
    string SampleId, string TenantId, string Scope, string CompanyId);
