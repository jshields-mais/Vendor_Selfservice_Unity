namespace Vss.Infrastructure.Erp;

/// <summary>Root ERP config (bound from the <c>Erp</c> section). Secrets are supplied
/// via user-secrets / env / k8s Secret — never in appsettings.</summary>
public class ErpOptions
{
    public const string Section = "Erp";

    /// <summary>Stub | SapByDesign | BusinessCentral.</summary>
    public string Provider { get; set; } = "Stub";

    public BusinessCentralOptions BusinessCentral { get; set; } = new();
    public SapByDesignOptions SapByDesign { get; set; } = new();
}

/// <summary>Dynamics 365 Business Central (REST / OAuth2 client-credentials).</summary>
public class BusinessCentralOptions
{
    /// <summary>API root through the company, e.g.
    /// https://api.businesscentral.dynamics.com/v2.0/{tenantId}/{environment}/api/v2.0</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Company GUID.</summary>
    public string CompanyId { get; set; } = "";

    // OAuth2 (Entra ID) client-credentials
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = ""; // secret
    public string Scope { get; set; } = "https://api.businesscentral.dynamics.com/.default";

    /// <summary>A known vendor number used by the connectivity test.</summary>
    public string SampleVendorNumber { get; set; } = "";
}

/// <summary>SAP Business ByDesign (SOAP / HTTP Basic via a Communication Arrangement).</summary>
public class SapByDesignOptions
{
    /// <summary>Tenant host, e.g. https://myXXXXXX.sapbydesign.com</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>QuerySupplierIn service path. [TODO: confirm against the sandbox WSDL]</summary>
    public string QuerySupplierPath { get; set; } = "/sap/bc/srt/scs/sap/querysupplierin";
    /// <summary>ManageSupplierIn service path. [TODO: confirm against the sandbox WSDL]</summary>
    public string ManageSupplierPath { get; set; } = "/sap/bc/srt/scs/sap/managesupplierin";

    public string Username { get; set; } = "";
    public string Password { get; set; } = ""; // secret

    /// <summary>A known supplier InternalID used by the connectivity test.</summary>
    public string SampleSupplierId { get; set; } = "";
}
