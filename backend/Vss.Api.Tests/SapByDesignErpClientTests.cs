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
            Fields = { ["LegalName"] = "Rocky Supply Co." }
        });

        var body = handler.Calls.Single().Body;
        Assert.Contains("SupplierBundleMaintainRequest_sync_V1", body);
        Assert.Contains("actionCode=\"04\"", body);
        Assert.Contains("<InternalID>V-10485</InternalID>", body);
        Assert.Contains("<FirstLineName>Rocky Supply Co.</FirstLineName>", body);
    }

    [Fact]
    public async Task GetVendor_maps_payment_method_and_active_bank_from_validity()
    {
        // Supplier pays by cheque (06) and has a superseded account (end-dated) plus the
        // current active account (validity covers "today", 2026-07-18 per the test clock).
        var response = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
          <n0:SupplierByElementsResponse_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
            <Supplier>
              <InternalID>62440</InternalID>
              <PaymentData><PaymentForm><PaymentFormCode>06</PaymentFormCode></PaymentForm></PaymentData>
              <BankDetails>
                <ID>0001</ID><BankRoutingID>021303618</BankRoutingID>
                <BankAccountID>111122223333</BankAccountID>
                <ValidityPeriod><StartDate>2000-01-01</StartDate><EndDate>2020-12-31</EndDate></ValidityPeriod>
              </BankDetails>
              <BankDetails>
                <ID>0002</ID><BankRoutingID>026009593</BankRoutingID>
                <BankAccountID>999888777</BankAccountID>
                <ValidityPeriod><StartDate>2021-01-01</StartDate><EndDate>9999-12-31</EndDate></ValidityPeriod>
              </BankDetails>
            </Supplier>
          </n0:SupplierByElementsResponse_sync>
        </soap:Body></soap:Envelope>
        """;
        var (client, _) = Make((_, _) => FakeHttpHandler.Xml(response));

        var v = await client.GetVendorAsync("62440");

        Assert.NotNull(v);
        Assert.Equal("Check", v!.PaymentMethod);
        // The active (unlimited) record, not the end-dated one.
        Assert.Equal("026009593", v.RoutingNumber);
        Assert.Equal("999888777", v.AccountNumber);
    }

    private const string MaintainOk = """
    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
      <n0:SupplierBundleMaintainConfirmation_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
        <Log><MaximumLogItemSeverityCode>1</MaximumLogItemSeverityCode></Log>
      </n0:SupplierBundleMaintainConfirmation_sync>
    </soap:Body></soap:Envelope>
    """;

    private static string SupplierWithBank(string bankXml) => $"""
    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
      <n0:SupplierByElementsResponse_sync xmlns:n0="http://sap.com/xi/SAPGlobal20/Global">
        <Supplier><InternalID>62440</InternalID>{bankXml}</Supplier>
      </n0:SupplierByElementsResponse_sync>
    </soap:Body></soap:Envelope>
    """;

    // Read-before-write posts a query first, then the maintain call. Route by body.
    private static (SapByDesignErpClient, FakeHttpHandler) MakeBankFlow(string queryResponse) =>
        Make((_, body) => FakeHttpHandler.Xml(
            body.Contains("SupplierByElementsQuery_sync") ? queryResponse : MaintainOk));

    private static readonly DateTimeOffset Approved = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private const string ExistingBank = """
      <BankDetails>
        <ID>0001</ID>
        <BankRoutingID>111000025</BankRoutingID>
        <BankRoutingIDTypeCode>ABA</BankRoutingIDTypeCode>
        <BankAccountID>23632465</BankAccountID>
        <ValidityPeriod><StartDate>2020-01-01</StartDate><EndDate>9999-12-31</EndDate></ValidityPeriod>
      </BankDetails>
    """;

    [Fact]
    public async Task Banking_new_account_no_prior_is_created_valid_from_approval_to_unlimited()
    {
        var (client, handler) = MakeBankFlow(SupplierWithBank("")); // no BankDetails on file

        await client.UpdateVendorMasterAsync("62440", new VendorMasterPatch
        {
            EffectiveDate = Approved,
            Fields = { ["AccountNumber"] = "55551111", ["RoutingNumber"] = "222000111" },
        });

        var body = handler.Calls.Last().Body;
        Assert.Contains("bankDetailsListCompleteTransmissionIndicator=\"true\"", body);
        Assert.Contains("actionCode=\"01\"", body);          // create
        Assert.Contains("<ID>0001</ID>", body);
        Assert.Contains("<BankAccountID>55551111</BankAccountID>", body);
        Assert.Contains("<StartDate>2026-07-18</StartDate>", body);
        Assert.Contains("<EndDate>9999-12-31</EndDate>", body);
    }

    [Fact]
    public async Task Banking_account_change_end_dates_prior_and_adds_new_record()
    {
        var (client, handler) = MakeBankFlow(SupplierWithBank(ExistingBank));

        await client.UpdateVendorMasterAsync("62440", new VendorMasterPatch
        {
            EffectiveDate = Approved,
            Fields = { ["AccountNumber"] = "99998888" },
        });

        var body = handler.Calls.Last().Body;
        // Prior record 0001 kept, end-dated the day before the new valid-from.
        Assert.Contains("<ID>0001</ID>", body);
        Assert.Contains("<BankAccountID>23632465</BankAccountID>", body);
        Assert.Contains("<EndDate>2026-07-17</EndDate>", body);
        // New record 0002 created, valid from approval to unlimited.
        Assert.Contains("actionCode=\"01\"", body);
        Assert.Contains("<ID>0002</ID>", body);
        Assert.Contains("<BankAccountID>99998888</BankAccountID>", body);
        Assert.Contains("<StartDate>2026-07-18</StartDate>", body);
        Assert.Contains("<EndDate>9999-12-31</EndDate>", body);
    }

    [Fact]
    public async Task Banking_same_account_updates_in_place_without_validity()
    {
        var (client, handler) = MakeBankFlow(SupplierWithBank(ExistingBank));

        await client.UpdateVendorMasterAsync("62440", new VendorMasterPatch
        {
            EffectiveDate = Approved,
            Fields = { ["RoutingNumber"] = "111000099" }, // routing correction, same account
        });

        var body = handler.Calls.Last().Body;
        Assert.Contains("<ID>0001</ID>", body);
        Assert.Contains("actionCode=\"04\"", body);         // in place
        Assert.Contains("<BankRoutingID>111000099</BankRoutingID>", body);
        Assert.DoesNotContain("ValidityPeriod", body);      // validity untouched
        Assert.DoesNotContain("<ID>0002</ID>", body);       // no new record
    }

    [Fact]
    public async Task AddAttachment_builds_attachmentfolder_envelope_with_base64_pdf()
    {
        var (client, handler) = Make((_, _) => FakeHttpHandler.Xml(MaintainOk));
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4 hello");

        var ok = await client.AddSupplierAttachmentAsync("62440",
            new ErpAttachment { FileName = "W9.pdf", MimeType = "application/pdf", Content = bytes });

        Assert.True(ok);
        var body = handler.Calls.Single().Body;
        Assert.Contains("<InternalID>62440</InternalID>", body);
        Assert.Contains("<AttachmentFolder", body);
        Assert.Contains("DocumentListCompleteTransmissionIndicator=\"false\"", body);
        Assert.Contains("ActionCode=\"01\"", body);                       // new document
        Assert.Contains("<CategoryCode>2</CategoryCode>", body);          // file attachment
        Assert.Contains("<TypeCode>10001</TypeCode>", body);
        Assert.Contains("<Name>W9.pdf</Name>", body);
        Assert.Contains("fileName=\"W9.pdf\"", body);
        Assert.Contains("mimeCode=\"application/pdf\"", body);
        Assert.Contains(Convert.ToBase64String(bytes), body);            // file content inline
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
