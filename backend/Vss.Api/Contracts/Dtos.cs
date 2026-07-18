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
public record AddressDto(string RemitStreet, string RemitCity, string RemitState, string RemitZip, string RemitCountry, string? PhysicalAddress);
public record BankingDto(string PaymentMethod, string? BankName, string? RoutingNumberMasked, string? AccountNumberMasked, string AccountType);
public record TaxDto(string? LegalTaxName, string TaxIdType, string? TinMasked, string? TaxClassification, string ExemptPayee, string? W9OnFile);
public record ContactsDto(string? PrimaryContact, string? PrimaryTitle, string? PrimaryEmail, string? PrimaryPhone, string? ApContactName, string? ApEmail, string? SalesContactName, string? SalesEmail);
public record DocumentDto(Guid Id, string Name, string? FileRef, string Validity, string Status);

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
    DocumentDto[] Documents);

// ---- Account linking ----
public record LinkRequestCreateDto(string Method, string? VendorNumber, string? Pin, string? TaxId, string? Zip);
public record LinkMatchResultDto(Guid? LinkRequestId, bool Matched, string? VendorNumber, string? VendorName, string? RemitCity, string? RemitState, string? RemitZip, string? TinMasked, string Status);

// ---- Change requests ----
public record ChangeDiffDto(string Field, string? FromValue, string? ToValue);
public record ChangeRequestCreateDto(string Section, ChangeDiffDto[] Diffs);
public record ChangeRequestDto(Guid Id, string Code, string VendorName, string Section, string SubmittedByName, DateTimeOffset SubmittedAt, string Status, ChangeDiffDto[] Diffs);
public record ReviewDecisionDto(string? Note);

// ---- Documents ----
public record DocumentUploadDto(string Name, string FileRef);

// ---- Admin ----
public record AdminLinkRequestDto(Guid Id, string Company, string Email, string Method, string? MatchedVendorNumber, DateTimeOffset CreatedAt, string Status);
public record AdminVendorDto(string Number, string Name, string Category, DateTimeOffset? LastSync, string Status);
public record AdminStatsDto(string ErpStatus, int PendingLinks, int PendingChanges, int LinkedVendors);
