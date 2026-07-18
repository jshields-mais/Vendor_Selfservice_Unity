using Vss.Domain;

namespace Vss.Infrastructure;

/// <summary>
/// Canonical demo data taken from the VSS prototype (City of Bozeman suppliers).
/// Single source of truth for both the portal database seed and the ERP stub, so
/// the two never drift.
/// </summary>
public static class SeedData
{
    // Stable ids so seeding is idempotent and the dev user can be pre-wired.
    public static readonly Guid NorthstarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DanaUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AcmeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static List<Vendor> Vendors() => new()
    {
        // Live SAP ByDesign test supplier (City of Jacksonville tenant) — the dev user is
        // pre-linked to this one so the SAP write round-trip is testable from the UI.
        new Vendor
        {
            Id = AcmeId,
            Number = "62440",
            Pin = "0000",
            LegalName = "ACME SPORTS INC",
            EntityType = "LLC",
            Status = "Active",
            RemitStreet = "",
            RemitCity = "SEYMOUR",
            RemitState = "IN",
            RemitZip = "",
            RemitCountry = "US",
            PaymentMethod = "ACH / EFT",
            TaxIdType = "EIN",
            LastSyncedAt = DateTimeOffset.UtcNow,
        },
        new Vendor
        {
            Id = NorthstarId,
            Number = "V-10485",
            Pin = "4820",
            LegalName = "Northstar Supply Co.",
            Dba = "Northstar Supply",
            EntityType = "LLC",
            Website = "www.northstarsupply.com",
            Status = "Active",
            RemitStreet = "1420 Industrial Blvd",
            RemitCity = "Bozeman",
            RemitState = "MT",
            RemitZip = "59715",
            RemitCountry = "United States",
            PhysicalAddress = "Same as remit-to",
            PaymentMethod = "ACH / EFT",
            BankName = "First Interstate Bank",
            RoutingNumber = "092900321",
            AccountNumber = "1004732",
            AccountType = "Checking",
            LegalTaxName = "Northstar Supply Co.",
            TaxIdType = "EIN",
            Tin = "81-3920423",
            TaxClassification = "S-Corporation",
            ExemptPayee = "No",
            W9OnFile = "Signed 02/14/2026",
            PrimaryContact = "Dana Whitfield",
            PrimaryTitle = "Account Manager",
            PrimaryEmail = "dana@northstarsupply.com",
            PrimaryPhone = "(406) 555-0192",
            ApContactName = "Marcus Lee",
            ApEmail = "ap@northstarsupply.com",
            SalesContactName = "Priya Anand",
            SalesEmail = "sales@northstarsupply.com",
            LastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            CategoryCodes = new()
            {
                new() { Code = "31000 · Industrial supplies" },
                new() { Code = "20500 · Fasteners & hardware" },
                new() { Code = "15800 · Safety equipment" },
                new() { Code = "75500 · Janitorial" },
                new() { Code = "48000 · Office supplies" },
            },
            Documents = new()
            {
                new() { Name = "W-9 form", FileRef = "W9_Northstar_2026.pdf", Validity = "No expiry", Status = DocumentStatus.Current },
                new() { Name = "Certificate of insurance", FileRef = "COI_2026.pdf", Validity = "Exp. 12/31/2026", Status = DocumentStatus.Current },
                new() { Name = "Business license", FileRef = "MT_license.pdf", Validity = "Exp. 08/15/2026", Status = DocumentStatus.Expiring },
                new() { Name = "W-8BEN (if foreign)", FileRef = null, Validity = "—", Status = DocumentStatus.AwaitingDocs },
            }
        },
        Simple("V-11204", "6501", "Gallatin Paper Co.", "Office & janitorial", "Bozeman", "MT", "59718", "45-2210987"),
        Simple("V-09920", "3372", "Rimrock Electric LLC", "Electrical", "Belgrade", "MT", "59714", "83-1177450"),
        Simple("V-10771", "9048", "Bridger Mechanical", "HVAC services", "Bozeman", "MT", "59715", "27-5540912"),
        Simple("V-08841", "1120", "Sourdough Fuels", "Fuel & lubricants", "Bozeman", "MT", "59715", "46-8890123"),
        Simple("V-12010", "7789", "Big Sky Signage", "Signage", "Livingston", "MT", "59047", "84-2093455"),
    };

    private static Vendor Simple(string number, string pin, string name, string category,
        string city, string state, string zip, string tin) => new()
    {
        Number = number,
        Pin = pin,
        LegalName = name,
        EntityType = "LLC",
        Status = "Active",
        RemitStreet = "—",
        RemitCity = city,
        RemitState = state,
        RemitZip = zip,
        PaymentMethod = "Check",
        TaxIdType = "EIN",
        Tin = tin,
        PrimaryEmail = $"ap@{name.ToLower().Replace(" ", "").Replace(".", "").Replace(",", "")}.com",
        LastSyncedAt = DateTimeOffset.UtcNow.AddHours(-1),
        CategoryCodes = new() { new() { Code = category } },
    };

    /// <summary>The demo vendor user (Dana Whitfield), pre-linked to the ACME SPORTS INC
    /// SAP supplier (62440) so the SAP write round-trip is testable straight from the UI.</summary>
    public static VendorUser DanaUser() => new()
    {
        Id = DanaUserId,
        ExternalUuid = "dev-dana-northstar",
        Email = "dana@northstarsupply.com",
        DisplayName = "Dana Whitfield",
        FirstName = "Dana",
        LastName = "Whitfield",
        LinkState = LinkState.Linked,
        VendorId = AcmeId,
    };
}
