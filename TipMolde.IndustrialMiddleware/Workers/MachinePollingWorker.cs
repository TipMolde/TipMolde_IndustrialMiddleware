using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Workers;

public sealed class MachinePollingWorker : BackgroundService
{
    private readonly IEnumerable<IMachineConnector> _connectors;
    private readonly IMachineTelemetryProcessor _telemetryProcessor;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<MachinePollingWorker> _logger;

    public MachinePollingWorker(
        IEnumerable<IMachineConnector> connectors,
        IMachineTelemetryProcessor telemetryProcessor,
        Microsoft.Extensions.Options.IOptions<IndustrialMiddlewareOptions> options,
        ILogger<MachinePollingWorker> logger)
    {
        _connectors = connectors;
        _telemetryProcessor = telemetryProcessor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Industrial middleware started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var connector in _connectors)
            {
                var rawItems = await connector.ReadAsync(stoppingToken);

                foreach (var raw in rawItems)
                {
                    try
                    {
                        await _telemetryProcessor.ProcessAsync(raw, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(
                            ex,
                            "Falha a processar a leitura de {MachineIp} ({Protocol}).",
                            raw.MachineIp,
                            raw.Protocol);
                    }
                }
            }
        }
    }
}
