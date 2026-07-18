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
    private static readonly XNamespace Glob = "http://sap.com/xi/SAPGlobal20/Global";

    public const string QueryAction = "http://sap.com/xi/SAPGlobal20/Global/QuerySupplierIn/FindByElementsRequest_sync";
    public const string ManageAction = "http://sap.com/xi/SAPGlobal20/Global/ManageSupplierIn/MaintainBundleRequest_sync";

    public static string BuildQueryByInternalId(string internalId) =>
        Envelope(new XElement(Glob + "SupplierByElementsQuery_sync",
            new XElement("SupplierSelectionByElements",
                new XElement("SelectionByInternalID",
                    new XElement("InclusionExclusionCode", "I"),
                    new XElement("IntervalBoundaryTypeCode", "1"),
                    new XElement("LowerBoundaryInternalID", internalId))),
            ProcessingConditions()));

    public static string BuildQueryByTaxId(string taxId) =>
        Envelope(new XElement(Glob + "SupplierByElementsQuery_sync",
            new XElement("SupplierSelectionByElements",
                new XElement("SelectionByPartyTaxID",
                    new XElement("InclusionExclusionCode", "I"),
                    new XElement("IntervalBoundaryTypeCode", "1"),
                    new XElement("LowerBoundaryPartyTaxID", taxId))),
            ProcessingConditions()));

    public static string BuildMaintainBundle(string internalId, IReadOnlyDictionary<string, string?> fields)
    {
        var supplier = new XElement("Supplier",
            new XAttribute("actionCode", "04"), // 04 = save/update
            new XElement("InternalID", internalId));

        if (fields.TryGetValue("LegalName", out var name) && name is not null)
            supplier.Add(new XElement("Organisation", new XElement("FirstLineName", name)));

        var addr = new XElement("PostalAddress");
        void AddAddr(string field, string el)
        {
            if (fields.TryGetValue(field, out var v) && v is not null) addr.Add(new XElement(el, v));
        }
        AddAddr("RemitStreet", "StreetName");
        AddAddr("RemitCity", "CityName");
        AddAddr("RemitState", "RegionCode");
        AddAddr("RemitZip", "StreetPostalCodeText");
        AddAddr("RemitCountry", "CountryCode");
        if (addr.HasElements) supplier.Add(new XElement("Address", addr));

        if (fields.TryGetValue("PrimaryEmail", out var email) && email is not null)
            supplier.Add(new XElement("Communication", new XElement("Email", new XElement("URI", email))));

        // Banking / tax writes are schema-heavy in ByDesign and left for WSDL-confirmed mapping.

        return Envelope(new XElement(Glob + "SupplierBundleMaintainRequest_sync",
            new XElement("BasicMessageHeader"),
            supplier));
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
