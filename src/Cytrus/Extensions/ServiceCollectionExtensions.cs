using Cytrus.Assembly;
using Cytrus.Cdn;
using Cytrus.Download;
using Cytrus.Manifest;
using Cytrus.Planning;
using Cytrus.Selection;
using Microsoft.Extensions.DependencyInjection;

namespace Cytrus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCytrus(this IServiceCollection services, Action<CdnOptions>? configure = null)
    {
        var options = new CdnOptions();

        configure?.Invoke(options);
        options.Validate();
        services.AddSingleton(options);

        services.AddTransient<RetryPolicy>();

        services.AddHttpClient<ICytrusCdnClient, CytrusCdnClient>((sp, client) =>
            {
                var o = sp.GetRequiredService<CdnOptions>();
                client.BaseAddress = o.BaseAddress;
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(static provider =>
            {
                var options = provider.GetRequiredService<CdnOptions>();

                return new SocketsHttpHandler
                {
                    ConnectTimeout = options.ConnectTimeout,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    EnableMultipleHttp2Connections = true,
                    MaxConnectionsPerServer = 64,
                    AllowAutoRedirect = false
                };
            });

        services.AddSingleton<IGameVersionResolver, GameVersionResolver>();
        services.AddSingleton<IManifestReader, FlatSharpManifestReader>();
        services.AddSingleton<IDownloadPlanner, DownloadPlanner>();
        services.AddSingleton<IFileSelectorFactory, GlobFileSelectorFactory>();
        services.AddSingleton<IFileAssembler, FileAssembler>();
        services.AddSingleton<IGameDownloader, GameDownloader>();

        return services;
    }
}
