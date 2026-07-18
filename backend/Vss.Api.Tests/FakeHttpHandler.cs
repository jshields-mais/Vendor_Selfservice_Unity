using System.Net;
using System.Text;

namespace Vss.Api.Tests;

/// <summary>Test double for HttpClient: records each request (with body) and returns
/// a canned response chosen by a responder delegate.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<(HttpRequestMessage Req, string Body)> Calls { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Calls.Add((request, body));
        return responder(request, body);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Xml(string xml, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };
}
