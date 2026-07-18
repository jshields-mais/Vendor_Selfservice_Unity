using Vss.Api.Contracts;
using Vss.Domain;

namespace Vss.Api.Mapping;

/// <summary>Maps domain entities to API DTOs and masks sensitive values for display.</summary>
public static class VendorMapping
{
    public static VendorDto ToDto(Vendor v) => new(
        v.Number,
        v.LegalName,
        v.Dba,
        v.EntityType,
        v.Website,
        v.Status,
        new AddressDto(v.RemitStreet, v.RemitCity, v.RemitState, v.RemitZip, v.RemitCountry, v.PhysicalAddress),
        new BankingDto(v.PaymentMethod, v.BankName, MaskMiddle(v.RoutingNumber, 3, 1), MaskTail(v.AccountNumber, 4), v.AccountType),
        new TaxDto(v.LegalTaxName, v.TaxIdType, MaskTin(v.Tin), v.TaxClassification, v.ExemptPayee, v.W9OnFile),
        new ContactsDto(v.PrimaryContact, v.PrimaryTitle, v.PrimaryEmail, v.PrimaryPhone, v.ApContactName, v.ApEmail, v.SalesContactName, v.SalesEmail),
        v.CategoryCodes.Select(c => c.Code).ToArray(),
        v.Documents.Select(d => new DocumentDto(d.Id, d.Name, d.FileRef, d.Validity, d.Status.ToString())).ToArray());

    /// <summary>Rough completeness score used for the "Profile complete" stat.</summary>
    public static int CompletenessPct(Vendor v)
    {
        var checks = new[]
        {
            !string.IsNullOrWhiteSpace(v.LegalName),
            !string.IsNullOrWhiteSpace(v.RemitStreet) && v.RemitStreet != "—",
            !string.IsNullOrWhiteSpace(v.BankName),
            !string.IsNullOrWhiteSpace(v.AccountNumber),
            !string.IsNullOrWhiteSpace(v.Tin),
            !string.IsNullOrWhiteSpace(v.PrimaryEmail),
            !string.IsNullOrWhiteSpace(v.W9OnFile),
            v.Documents.Any(d => d.Status == DocumentStatus.Current),
        };
        return (int)Math.Round(100.0 * checks.Count(x => x) / checks.Length);
    }

    // ---- masking ----
    private static string? MaskTail(string? value, int visible)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= visible) return new string('•', value.Length);
        return new string('•', value.Length - visible) + value[^visible..];
    }

    private static string? MaskMiddle(string? value, int start, int end)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= start + end) return new string('•', value.Length);
        return value[..start] + new string('•', value.Length - start - end) + value[^end..];
    }

    public static string? MaskTin(string? tin)
    {
        if (string.IsNullOrEmpty(tin)) return tin;
        var digits = tin.Replace("-", "");
        if (digits.Length < 4) return tin;
        // Keep the first 2 and last 2 characters of the original formatting.
        return tin[..2] + "-•••••" + tin[^2..];
    }
}
