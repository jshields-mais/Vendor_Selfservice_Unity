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
        // Schema order: CountryCode, [StreetName, HouseID | ...], CityName, RegionCode,
        // StreetPostalCode, ..., POBoxIndicator, POBoxID, POBoxPostalCode. A PO Box carries
        // the box number/postal code in the PO Box fields instead of the street fields.
        var isPoBox = string.Equals(fields.GetValueOrDefault("IsPoBox"), "true", StringComparison.OrdinalIgnoreCase);
        P("RemitCountry", "CountryCode");
        if (!isPoBox)
        {
            P("RemitStreet", "StreetName");
            P("HouseNumber", "HouseID");
        }
        P("RemitCity", "CityName");
        P("RemitState", "RegionCode");
        if (isPoBox)
        {
            postal.Add(new XElement("POBoxIndicator", "true"));
            P("PoBox", "POBoxID");
            P("RemitZip", "POBoxPostalCode");
        }
        else
        {
            P("RemitZip", "StreetPostalCode");
        }
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

    /// <summary>
    /// Builds a MaintainBundle that appends one document to the supplier's attachment
    /// folder (the "Attachments" tab). The folder is sent as an incomplete list
    /// (DocumentListCompleteTransmissionIndicator=false) so existing attachments are kept.
    /// Note: AttachmentFolder/Document/FileContent use a capitalised <c>ActionCode</c>
    /// attribute, unlike the lowercase <c>actionCode</c> on Supplier/Address/BankDetails.
    /// </summary>
    public static string BuildAddAttachment(string internalId, string fileName, string mimeCode, string base64Content)
    {
        var supplier = new XElement("Supplier",
            new XAttribute("actionCode", "04"),
            new XElement("InternalID", internalId),
            new XElement("AttachmentFolder",
                new XAttribute("ActionCode", "04"),
                new XAttribute("DocumentListCompleteTransmissionIndicator", "false"),
                new XElement("Document",
                    new XAttribute("ActionCode", "01"),
                    new XElement("VisibleIndicator", "true"),
                    // CategoryCode 2 + TypeCode 10001 = a standard file attachment; values
                    // confirmed by reading an existing attachment off a live supplier.
                    new XElement("CategoryCode", "2"),
                    new XElement("TypeCode", "10001"),
                    new XElement("MIMECode", mimeCode),
                    new XElement("Name", fileName),
                    new XElement("FileContent",
                        new XAttribute("ActionCode", "01"),
                        new XElement("BinaryObject",
                            new XAttribute("mimeCode", mimeCode),
                            new XAttribute("fileName", fileName),
                            base64Content)))));

        return Envelope(new XElement(Glob + "SupplierBundleMaintainRequest_sync_V1",
            new XElement("BasicMessageHeader"),
            supplier));
    }

    /// <summary>
    /// Builds a MaintainBundle setting per-document communication preferences. Each entry is a
    /// CommunicationArrangement: the business document (CompoundServiceInterfaceCode), an
    /// EnabledIndicator, the channel (CommunicationMediumTypeCode) and, for email, the address.
    /// </summary>
    public static string BuildCommunicationArrangements(string internalId,
        IEnumerable<(string ServiceInterfaceCode, string MediumCode, string? Email, bool Enabled)> arrangements)
    {
        var supplier = new XElement("Supplier",
            new XAttribute("actionCode", "04"),
            new XElement("InternalID", internalId));

        foreach (var a in arrangements)
        {
            var arr = new XElement("CommunicationArrangement",
                new XAttribute("actionCode", "04"),
                new XElement("CompoundServiceInterfaceCode", a.ServiceInterfaceCode),
                new XElement("EnabledIndicator", a.Enabled ? "true" : "false"),
                new XElement("CommunicationMediumTypeCode", a.MediumCode));
            if (a.Enabled && a.MediumCode == "INT" && !string.IsNullOrEmpty(a.Email))
                arr.Add(new XElement("EMailURI", a.Email));
            supplier.Add(arr);
        }

        return Envelope(new XElement(Glob + "SupplierBundleMaintainRequest_sync_V1",
            new XElement("BasicMessageHeader"),
            supplier));
    }

    /// <summary>
    /// Builds a MaintainBundle for a supplier ContactPerson. <paramref name="actionCode"/> is
    /// "01" to create (no keys), "04" to update in place (keys required), "03" to delete.
    /// Names map to GivenName/FamilyName; title/function/department to coded fields;
    /// email/fax/phone/mobile to the Workplace* fields (phone+mobile as a complete list, LCTI).
    /// </summary>
    public static string BuildContactPerson(string internalId, SapContact c, string actionCode = "04")
    {
        var cp = new XElement("ContactPerson", new XAttribute("actionCode", actionCode));
        void Add(string el, string? val) { if (val is not null) cp.Add(new XElement(el, val)); }

        // Delete needs only the record key.
        if (actionCode == "03")
        {
            if (!string.IsNullOrEmpty(c.Uuid)) cp.Add(new XElement("BusinessPartnerContactUUID", c.Uuid));
            if (!string.IsNullOrEmpty(c.InternalId)) cp.Add(new XElement("BusinessPartnerContactInternalID", c.InternalId));
            var delSup = new XElement("Supplier", new XAttribute("actionCode", "04"),
                new XElement("InternalID", internalId), cp);
            return Envelope(new XElement(Glob + "SupplierBundleMaintainRequest_sync_V1",
                new XElement("BasicMessageHeader"), delSup));
        }

        // schema order: UUID, InternalID, FormOfAddressCode, GivenName, FamilyName,
        // BusinessPartnerFunctionTypeCode, BusinessPartnerFunctionalAreaCode, WorkplaceEMailURI,
        // WorkplaceFacsimile..., WorkplaceTelephone*. Title/Function/Department are coded (see SapContact).
        if (!string.IsNullOrEmpty(c.Uuid)) cp.Add(new XElement("BusinessPartnerContactUUID", c.Uuid));
        if (!string.IsNullOrEmpty(c.InternalId)) cp.Add(new XElement("BusinessPartnerContactInternalID", c.InternalId));
        Add("FormOfAddressCode", c.FormOfAddressCode);
        Add("GivenName", c.FirstName);
        Add("FamilyName", c.LastName);
        Add("BusinessPartnerFunctionTypeCode", c.FunctionCode);
        Add("BusinessPartnerFunctionalAreaCode", c.DepartmentCode);
        Add("WorkplaceEMailURI", c.Email);
        Add("WorkplaceFacsimileFormattedNumberDescription", c.Fax);
        if (c.Phone is not null || c.Mobile is not null)
        {
            cp.Add(new XAttribute("workplaceTelephoneListCompleteTransmissionIndicator", "true"));
            if (!string.IsNullOrEmpty(c.Phone))
                cp.Add(new XElement("WorkplaceTelephone", new XElement("FormattedNumberDescription", c.Phone), new XElement("MobilePhoneNumberIndicator", "false")));
            if (!string.IsNullOrEmpty(c.Mobile))
                cp.Add(new XElement("WorkplaceTelephone", new XElement("FormattedNumberDescription", c.Mobile), new XElement("MobilePhoneNumberIndicator", "true")));
        }

        var supplier = new XElement("Supplier",
            new XAttribute("actionCode", "04"),
            new XElement("InternalID", internalId),
            cp);
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

/// <summary>Effective contact values for a ContactPerson update-in-place write.</summary>
public sealed class SapContact
{
    public string? Uuid { get; init; }
    public string? InternalId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    /// <summary>Title → SAP FormOfAddressCode (coded salutation).</summary>
    public string? FormOfAddressCode { get; init; }
    /// <summary>Function → SAP BusinessPartnerFunctionTypeCode (coded).</summary>
    public string? FunctionCode { get; init; }
    /// <summary>Department → SAP BusinessPartnerFunctionalAreaCode (coded).</summary>
    public string? DepartmentCode { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? Fax { get; init; }
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
