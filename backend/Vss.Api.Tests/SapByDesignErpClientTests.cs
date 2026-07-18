using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Vss.Infrastructure.Erp;
using Vss.Infrastructure.Erp.SapByDesign;
using Xunit;

namespace Vss.Api.Tests;

public class SapByDesignErpClientTests
{
    private const string QueryResponse = """
    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
      <soap:Body>
        <n0:SupplierByElementsResponse_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
          <Supplier>
            <InternalID>V-10485</InternalID>
            <Organisation><FirstLineName>Northstar Supply Co.</FirstLineName></Organisation>
            <Address>
              <StreetName>1420 Industrial Blvd</StreetName>
              <CityName>Bozeman</CityName>
              <RegionCode>MT</RegionCode>
              <StreetPostalCode>59715</StreetPostalCode>
              <CountryCode>US</CountryCode>
            </Address>
            <TaxID>81-3920423</TaxID>
          </Supplier>
        </n0:SupplierByElementsResponse_sync>
      </soap:Body>
    </soap:Envelope>
    """;

    private static (SapByDesignErpClient client, FakeHttpHandler handler) Make(
        Func<HttpRequestMessage, string, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpHandler(responder);
        var http = new HttpClient(handler);
        var opt = new SapByDesignOptions { BaseUrl = "https://byd.test", Username = "u", Password = "p" };
        return (new SapByDesignErpClient(http, opt, NullLogger<SapByDesignErpClient>.Instance), handler);
    }

    [Fact]
    public async Task GetVendor_parses_supplier_and_sends_basic_auth_and_soapaction()
    {
        var (client, handler) = Make((_, _) => FakeHttpHandler.Xml(QueryResponse));

        var v = await client.GetVendorAsync("V-10485");

        Assert.NotNull(v);
        Assert.Equal("V-10485", v!.Number);
        Assert.Equal("Northstar Supply Co.", v.LegalName);
        Assert.Equal("Bozeman", v.RemitCity);
        Assert.Equal("59715", v.RemitZip);
        Assert.Equal("81-3920423", v.Tin);

        var call = handler.Calls.Single();
        Assert.Equal("Basic", call.Req.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.ASCII.GetBytes("u:p")), call.Req.Headers.Authorization!.Parameter);
        Assert.True(call.Req.Headers.Contains("SOAPAction"));
        Assert.Contains("SupplierByElementsQuery_sync", call.Body);
        Assert.Contains("V-10485", call.Body);
    }

    [Fact]
    public async Task Update_builds_maintainbundle_envelope()
    {
        var ok = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
          <n0:SupplierBundleMaintainConfirmation_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
            <Log><MaximumLogItemSeverityCode>1</MaximumLogItemSeverityCode></Log>
          </n0:SupplierBundleMaintainConfirmation_sync>
        </soap:Body></soap:Envelope>
        """;
        var (client, handler) = Make((_, _) => FakeHttpHandler.Xml(ok));

        await client.UpdateVendorMasterAsync("V-10485", new VendorMasterPatch
        {
            Fields = { ["LegalName"] = "Rocky Supply Co.", ["RemitCity"] = "Belgrade" }
        });

        var body = handler.Calls.Single().Body;
        Assert.Contains("SupplierBundleMaintainRequest_sync", body);
        Assert.Contains("actionCode=\"04\"", body);
        Assert.Contains("<InternalID>V-10485</InternalID>", body);
        Assert.Contains("Rocky Supply Co.", body);
        Assert.Contains("Belgrade", body);
    }

    [Fact]
    public async Task Update_throws_on_error_severity()
    {
        var err = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
          <n0:SupplierBundleMaintainConfirmation_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
            <Log><MaximumLogItemSeverityCode>3</MaximumLogItemSeverityCode>
              <Item><Note>Supplier is blocked</Note></Item>
            </Log>
          </n0:SupplierBundleMaintainConfirmation_sync>
        </soap:Body></soap:Envelope>
        """;
        var (client, _) = Make((_, _) => FakeHttpHandler.Xml(err));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.UpdateVendorMasterAsync("V-10485", new VendorMasterPatch { Fields = { ["LegalName"] = "X" } }));
        Assert.Contains("Supplier is blocked", ex.Message);
    }
}
