using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vss.Infrastructure.Erp.BusinessCentral;
using Vss.Infrastructure.Erp.SapByDesign;

namespace Vss.Infrastructure.Erp;

public static class ErpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ERP client selected by <c>Erp:Provider</c> (Stub | SapByDesign |
    /// BusinessCentral). Defaults to <see cref="StubErpClient"/>, so nothing changes for
    /// dev/tests unless the provider is set.
    /// </summary>
    public static IServiceCollection AddErpClient(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ErpOptions>(config.GetSection(ErpOptions.Section));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ErpOptions>>().Value.BusinessCentral);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ErpOptions>>().Value.SapByDesign);

        var provider = (config.GetSection(ErpOptions.Section)["Provider"] ?? "Stub").Trim().ToLowerInvariant();
        switch (provider)
        {
            case "businesscentral":
                services.AddSingleton<IBcTokenProvider, MsalBcTokenProvider>();
                services.AddHttpClient<IErpClient, BusinessCentralErpClient>();
                break;
            case "sapbydesign":
                services.AddHttpClient<IErpClient, SapByDesignErpClient>();
                break;
            default:
                services.AddSingleton<IErpClient, StubErpClient>();
                break;
        }
        return services;
    }
}
