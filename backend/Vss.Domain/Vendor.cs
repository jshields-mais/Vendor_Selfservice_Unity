namespace Vss.Domain;

/// <summary>
/// A supplier record that mirrors the City's ERP vendor master. This is the
/// canonical shape the portal reads and proposes changes against; the ERP is
/// still the system of record (see <c>IErpClient</c>).
/// </summary>
public class Vendor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ERP vendor number, e.g. "V-10485".</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Verification PIN used with <see cref="LinkMethod.VendorNumberPin"/>.</summary>
    public string Pin { get; set; } = string.Empty;

    // ---- Company ----
    public string LegalName { get; set; } = string.Empty;
    public string? Dba { get; set; }
    public string EntityType { get; set; } = "LLC";
    public string? Website { get; set; }
    public string Status { get; set; } = "Active";

    // ---- Remit-to / physical address ----
    public string RemitStreet { get; set; } = string.Empty;
    public string RemitCity { get; set; } = string.Empty;
    public string RemitState { get; set; } = string.Empty;
    public string RemitZip { get; set; } = string.Empty;
    public string RemitCountry { get; set; } = "United States";
    public string? PhysicalAddress { get; set; }

    // ---- Banking / remittance (edits always require review) ----
    public string PaymentMethod { get; set; } = "ACH / EFT";
    public string? BankName { get; set; }
    public string? RoutingNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string AccountType { get; set; } = "Checking";

    // ---- Tax / W-9 (edits always require review) ----
    public string? LegalTaxName { get; set; }
    public string TaxIdType { get; set; } = "EIN";
    public string? Tin { get; set; }
    public string? TaxClassification { get; set; }
    public string ExemptPayee { get; set; } = "No";
    public string? W9OnFile { get; set; }

    // ---- Contacts ----
    public string? PrimaryContact { get; set; }
    public string? PrimaryTitle { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? ApContactName { get; set; }
    public string? ApEmail { get; set; }
    public string? SalesContactName { get; set; }
    public string? SalesEmail { get; set; }

    // ---- Category / classification ----
    public List<VendorCategoryCode> CategoryCodes { get; set; } = new();

    // ---- Compliance documents ----
    public List<VendorDocument> Documents { get; set; } = new();

    public DateTimeOffset? LastSyncedAt { get; set; }
}
