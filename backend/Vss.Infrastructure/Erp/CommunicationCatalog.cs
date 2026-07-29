namespace Vss.Infrastructure.Erp;

/// <summary>
/// The business documents and delivery channels offered for supplier communication
/// preferences, plus their SAP ByDesign codes. A preference maps to a
/// <c>CommunicationArrangement</c> (CompoundServiceInterfaceCode + CommunicationMediumTypeCode).
///
/// Only Purchase Order (11) and the channel codes are confirmed; other documents carry an
/// empty service-interface code until their CompoundServiceInterfaceCode is confirmed, and
/// are recorded as portal preferences without an ERP write (ErpEnabled = false).
/// </summary>
public static class CommunicationCatalog
{
    public record BusinessDocument(string Name, string ServiceInterfaceCode)
    {
        public bool ErpEnabled => !string.IsNullOrEmpty(ServiceInterfaceCode);
    }

    public static readonly IReadOnlyList<BusinessDocument> Documents = new[]
    {
        new BusinessDocument("Purchase Order", "11"),            // confirmed
        new BusinessDocument("Purchase Order Acknowledgment", ""),
        new BusinessDocument("Request for Quotation", ""),
        new BusinessDocument("Invoice", ""),
        new BusinessDocument("Remittance Advice", ""),
    };

    /// <summary>Channel display → CommunicationMediumTypeCode (INT/e-mail confirmed).</summary>
    public static readonly IReadOnlyDictionary<string, string> ChannelMedium = new Dictionary<string, string>
    {
        ["Email"] = "INT",
        ["Print"] = "LET",
        ["Fax"] = "FAX",
        ["XML / EDI"] = "XML",
    };

    public static IReadOnlyList<string> Channels => ChannelMedium.Keys.ToArray();

    public static string? ServiceInterfaceCodeFor(string document) =>
        Documents.FirstOrDefault(d => d.Name == document)?.ServiceInterfaceCode is { Length: > 0 } c ? c : null;

    public static string? MediumCodeFor(string channel) => ChannelMedium.GetValueOrDefault(channel);
}
