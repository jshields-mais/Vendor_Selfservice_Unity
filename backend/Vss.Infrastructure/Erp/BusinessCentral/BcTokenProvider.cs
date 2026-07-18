using Microsoft.Identity.Client;

namespace Vss.Infrastructure.Erp.BusinessCentral;

/// <summary>Supplies an OAuth2 access token for Business Central. Abstracted so tests
/// can bypass the real Entra ID token endpoint.</summary>
public interface IBcTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}

/// <summary>Client-credentials token via MSAL (Entra ID). MSAL caches tokens per app
/// instance and refreshes them near expiry.</summary>
internal sealed class MsalBcTokenProvider(BusinessCentralOptions options) : IBcTokenProvider
{
    private IConfidentialClientApplication? _app;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        // Built lazily so a misconfigured provider fails on the call (caught + reported),
        // not at DI construction time.
        _app ??= ConfidentialClientApplicationBuilder.Create(options.ClientId)
            .WithClientSecret(options.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}")
            .Build();

        var result = await _app.AcquireTokenForClient(new[] { options.Scope }).ExecuteAsync(ct);
        return result.AccessToken;
    }
}
