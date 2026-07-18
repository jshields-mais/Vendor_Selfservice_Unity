using System.Xml.Linq;

namespace Vss.Infrastructure.Erp.SapByDesign;

/// <summary>
/// SOAP namespaces, SOAPAction values, and envelope builders for ByDesign
/// QuerySupplierIn / ManageSupplierIn. These are the common A2X shapes; the exact
/// element namespaces + write-side nesting are tenant/WSDL-specific.
/// [TODO: confirm all values in this file against the sandbox WSDL / sample payloads.]
/// </summary>
internal static class Sap
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    // Confirmed against the COJ WSDLs + a live call: the message BODY elements are in
    // SAPGlobal20/Global, while the SOAPAction uses the A1S/Global service namespace.
    private static readonly XNamespace Glob = "http://sap.com/xi/SAPGlobal20/Global";

    public const string QueryAction = "http://sap.com/xi/A1S/Global/QuerySupplierIn/FindByElementsRequest";
    public const string ManageAction = "http://sap.com/xi/A1S/Global/ManageSupplierIn/MaintainBundle_V1Request";

    // SelectionByInternalID has type SelectionByIdentifier, so its boundary element is
    // LowerBoundaryIdentifier (NOT LowerBoundaryInternalID). IntervalBoundaryTypeCode 1 = equal.
    public static string BuildQueryByInternalId(string internalId) =>
        Envelope(new XElement(Glob + "SupplierByElementsQuery_sync",
            new XElement("SupplierSelectionByElements",
                new XElement("SelectionByInternalID",
                    new XElement("InclusionExclusionCode", "I"),
                    new XElement("IntervalBoundaryTypeCode", "1"),
                    new XElement("LowerBoundaryIdentifier", internalId))),
            ProcessingConditions()));

    /// <summary>
    /// Builds a MaintainBundle_V1 update. Name (FirstLineName) sits directly on the
    /// supplier bundle. Address + email/phone live under AddressInformation, which
    /// ByDesign only accepts as a complete list (LCTI) and must carry the existing
    /// address UUID so the in-use address is updated in place, not deleted/recreated.
    /// Element order follows the WSDL schema sequence.
    /// </summary>
    public static string BuildMaintainBundle(string internalId, IReadOnlyDictionary<string, string?> fields, SapMaintainContext? ctx = null)
    {
        ctx ??= new SapMaintainContext();
        var supplier = new XElement("Supplier",
            new XAttribute("actionCode", "04"),
            new XElement("InternalID", internalId));

        if (fields.TryGetValue("LegalName", out var name) && name is not null)
            supplier.Add(new XElement("FirstLineName", name));

        // ---- Address + email/phone (AddressInformation, LCTI + UUID) ----
        var address = new XElement("Address", new XAttribute("actionCode", "04"));
        var postal = new XElement("PostalAddress");
        void P(string field, string el)
        {
            if (fields.TryGetValue(field, out var v) && !string.IsNullOrEmpty(v)) postal.Add(new XElement(el, v));
        }
        P("RemitCountry", "CountryCode"); // schema order: Country, Street, City, Region, PostalCode
        P("RemitStreet", "StreetName");
        P("RemitCity", "CityName");
        P("RemitState", "RegionCode");
        P("RemitZip", "StreetPostalCode");
        if (postal.HasElements) address.Add(postal);
        if (fields.TryGetValue("PrimaryPhone", out var phone) && !string.IsNullOrEmpty(phone))
            address.Add(new XElement("PhoneFormattedNumberDescription", phone));
        if (fields.TryGetValue("PrimaryEmail", out var email) && !string.IsNullOrEmpty(email))
            address.Add(new XElement("EMailURI", email));

        if (address.HasElements && !string.IsNullOrEmpty(ctx.AddressUuid))
        {
            supplier.Add(new XAttribute("addressInformationListCompleteTransmissionIndicator", "true"));
            supplier.Add(new XElement("AddressInformation",
                new XAttribute("actionCode", "04"),
                new XElement("UUID", ctx.AddressUuid),
                address));
        }

        // ---- Banking (BankDetails, LCTI + existing record keys; routing must resolve to a
        //      bank in the ByDesign bank directory). SAP keeps bank accounts as validity-
        //      dated records, so a bank *change* end-dates the prior record and adds a new
        //      one rather than overwriting in place. schema order per record: ID,
        //      BankRoutingID, BankRoutingIDTypeCode, BankAccountID, ValidityPeriod. ----
        if (ctx.Bank is { } bank)
        {
            supplier.Add(new XAttribute("bankDetailsListCompleteTransmissionIndicator", "true"));
            // End-date the outgoing account first (bank change A -> B), then the new/active one.
            if (bank.Prior is { } prior)
                supplier.Add(BankElement("04", prior.Id, prior.RoutingId, prior.RoutingIdTypeCode,
                    prior.AccountId, prior.ValidFrom, prior.ValidTo));
            supplier.Add(BankElement(bank.ActionCode, bank.Id, bank.RoutingId, bank.RoutingIdTypeCode,
                bank.AccountId, bank.WriteValidity ? bank.ValidFrom : null, bank.WriteValidity ? bank.ValidTo : null));
        }

        return Envelope(new XElement(Glob + "SupplierBundleMaintainRequest_sync_V1",
            new XElement("BasicMessageHeader"),
            supplier));
    }

    /// <summary>SAP's high date for an unlimited "valid to" (9999-12-31).</summary>
    public static readonly DateOnly UnlimitedDate = new(9999, 12, 31);

    private static XElement BankElement(string actionCode, string id, string? routing, string? routingType,
        string? account, DateOnly? validFrom, DateOnly? validTo)
    {
        var el = new XElement("BankDetails",
            new XAttribute("actionCode", actionCode),
            new XElement("ID", id));
        if (!string.IsNullOrEmpty(routing))
        {
            el.Add(new XElement("BankRoutingID", routing));
            // US ABA routing standard (default when the source record has no type).
            el.Add(new XElement("BankRoutingIDTypeCode", string.IsNullOrEmpty(routingType) ? "ABA" : routingType));
        }
        if (!string.IsNullOrEmpty(account))
            el.Add(new XElement("BankAccountID", account));
        if (validFrom is not null && validTo is not null)
            el.Add(new XElement("ValidityPeriod",
                new XElement("StartDate", validFrom.Value.ToString("yyyy-MM-dd")),
                new XElement("EndDate", validTo.Value.ToString("yyyy-MM-dd"))));
        return el;
    }

    private static XElement ProcessingConditions() =>
        new("ProcessingConditions",
            new XElement("QueryHitsMaximumNumberValue", "1"),
            new XElement("QueryHitsUnlimitedIndicator", "false"));

    private static string Envelope(XElement body) =>
        new XDocument(
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", Soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "glob", Glob.NamespaceName),
                new XElement(Soap + "Header"),
                new XElement(Soap + "Body", body)))
            .ToString(SaveOptions.DisableFormatting);
}

/// <summary>Identifiers read from the supplier before a write so update-in-place nodes
/// (address, bank details) can carry the existing record keys required by ByDesign.</summary>
internal sealed class SapMaintainContext
{
    public string? AddressUuid { get; set; }

    /// <summary>The bank-detail write to emit, resolved from the current supplier state
    /// (create new / update in place / end-date-and-replace). Null when banking is untouched.</summary>
    public SapBankMaintain? Bank { get; set; }
}

/// <summary>The active bank-detail record to write, plus (for a bank change) the prior
/// record to end-date. Validity dates realise SAP's "valid from / valid to" on bank data.</summary>
internal sealed class SapBankMaintain
{
    public required string Id { get; init; }
    /// <summary>"01" to create a new record, "04" to update the existing one in place.</summary>
    public required string ActionCode { get; init; }
    public string? RoutingId { get; init; }
    public string? RoutingIdTypeCode { get; init; }
    public string? AccountId { get; init; }
    /// <summary>Emit a ValidityPeriod for the active record (new/changed account only).</summary>
    public bool WriteValidity { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly ValidTo { get; init; }
    /// <summary>Outgoing account to end-date when the vendor switches banks (A -> B).</summary>
    public SapBankPriorClose? Prior { get; init; }
}

/// <summary>An existing bank record being closed out: its keys re-sent unchanged with a new
/// valid-to (the day before the incoming account's valid-from).</summary>
internal sealed class SapBankPriorClose
{
    public required string Id { get; init; }
    public string? RoutingId { get; init; }
    public string? RoutingIdTypeCode { get; init; }
    public string? AccountId { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly ValidTo { get; init; }
}
