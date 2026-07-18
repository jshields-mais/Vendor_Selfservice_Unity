using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Vss.Infrastructure.Erp;
using Vss.Infrastructure.Erp.BusinessCentral;
using Xunit;

namespace Vss.Api.Tests;

public class BusinessCentralErpClientTests
{
    private const string VendorJson = """
    { "value": [ {
      "@odata.etag": "W/\"JzQ0OzExMTs=\"",
      "id": "aaaaaaaa-1111-2222-3333-444444444444",
      "number": "V-10485",
      "displayName": "Northstar Supply Co.",
      "phoneNumber": "(406) 555-0192",
      "email": "dana@northstarsupply.com",
      "taxRegistrationNumber": "81-3920423",
      "blocked": " ",
      "address": { "street": "1420 Industrial Blvd", "city": "Bozeman", "state": "MT", "postalCode": "59715", "countryRegionCode": "US" }
    } ] }
    """;

    private sealed class FakeToken : IBcTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken ct = default) => Task.FromResult("test-token");
    }

    private static (BusinessCentralErpClient client, FakeHttpHandler handler) Make(
        Func<HttpRequestMessage, string, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpHandler(responder);
        var http = new HttpClient(handler);
        var opt = new BusinessCentralOptions { BaseUrl = "https://bc.test/api/v2.0", CompanyId = "co-1" };
        return (new BusinessCentralErpClient(http, opt, new FakeToken(), NullLogger<BusinessCentralErpClient>.Instance), handler);
    }

    [Fact]
    public async Task GetVendor_maps_fields_and_sends_bearer_and_filter()
    {
        var (client, handler) = Make((_, _) => FakeHttpHandler.Json(VendorJson));

        var v = await client.GetVendorAsync("V-10485");

        Assert.NotNull(v);
        Assert.Equal("V-10485", v!.Number);
        Assert.Equal("Northstar Supply Co.", v.LegalName);
        Assert.Equal("Bozeman", v.RemitCity);
        Assert.Equal("59715", v.RemitZip);
        Assert.Equal("81-3920423", v.Tin);

        var call = handler.Calls.Single();
        Assert.Equal(HttpMethod.Get, call.Req.Method);
        Assert.Contains("companies(co-1)/vendors", call.Req.RequestUri!.ToString());
        Assert.Contains("number eq 'V-10485'", Uri.UnescapeDataString(call.Req.RequestUri!.Query));
        Assert.Equal("Bearer", call.Req.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", call.Req.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Update_patches_by_id_with_ifmatch_and_maps_fields_skipping_banking()
    {
        var (client, handler) = Make((req, _) =>
            req.Method == HttpMethod.Patch ? new HttpResponseMessage(HttpStatusCode.NoContent)
                                           : FakeHttpHandler.Json(VendorJson));

        await client.UpdateVendorMasterAsync("V-10485", new VendorMasterPatch
        {
            Fields = { ["LegalName"] = "Rocky Supply Co.", ["RemitStreet"] = "1 New St", ["BankName"] = "Rocky Bank" }
        });

        var patch = handler.Calls.Single(c => c.Req.Method == HttpMethod.Patch);
        Assert.Contains("vendors(aaaaaaaa-1111-2222-3333-444444444444)", patch.Req.RequestUri!.ToString());
        Assert.Equal("W/\"JzQ0OzExMTs=\"", patch.Req.Headers.GetValues("If-Match").Single());
        Assert.Contains("\"displayName\":\"Rocky Supply Co.\"", patch.Body);
        Assert.Contains("\"street\":\"1 New St\"", patch.Body);
        Assert.DoesNotContain("BankName", patch.Body); // banking not on the vendor entity
    }

    [Fact]
    public async Task Match_by_taxid_and_zip()
    {
        var (client, _) = Make((_, _) => FakeHttpHandler.Json(VendorJson));

        var hit = await client.MatchVendorAsync(new MatchQuery { Method = "TaxIdZip", TaxId = "81-3920423", Zip = "59715" });
        Assert.Equal("V-10485", hit!.Number);

        var (client2, _) = Make((_, _) => FakeHttpHandler.Json(VendorJson));
        var miss = await client2.MatchVendorAsync(new MatchQuery { Method = "TaxIdZip", TaxId = "81-3920423", Zip = "00000" });
        Assert.Null(miss);
    }
}
