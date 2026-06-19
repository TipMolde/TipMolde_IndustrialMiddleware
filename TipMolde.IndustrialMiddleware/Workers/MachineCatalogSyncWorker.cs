using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Workers;

public sealed class MachineCatalogSyncWorker : BackgroundService
{
    private readonly IMachineCatalogClient _client;
    private readonly IMachineCatalogStore _store;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<MachineCatalogSyncWorker> _logger;

    public MachineCatalogSyncWorker(
        IMachineCatalogClient client,
        IMachineCatalogStore store,
        Microsoft.Extensions.Options.IOptions<IndustrialMiddlewareOptions> options,
        ILogger<MachineCatalogSyncWorker> logger)
    {
        _client = client;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.MachineCatalogRefreshMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncAsync(stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            var targets = await _client.GetMachineTargetsAsync(stoppingToken);
            _store.Replace(targets);
            _logger.LogInformation(
                "Catalogo de maquinas sincronizado. Total de maquinas ativas com IP: {Count}",
                targets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha a sincronizar o catalogo de maquinas a partir do backend.");
        }
    }
}
