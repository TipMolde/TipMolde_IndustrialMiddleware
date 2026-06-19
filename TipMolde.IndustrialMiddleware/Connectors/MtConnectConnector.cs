using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Connectors;

public sealed class MtConnectConnector : IMachineConnector
{
    private readonly HttpClient _httpClient;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly IMachineCatalogStore _machineCatalogStore;
    private readonly ILogger<MtConnectConnector> _logger;

    public MtConnectConnector(
        HttpClient httpClient,
        IOptions<IndustrialMiddlewareOptions> options,
        IMachineCatalogStore machineCatalogStore,
        ILogger<MtConnectConnector> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _machineCatalogStore = machineCatalogStore;
        _logger = logger;
    }

    public string Protocol => "MTConnect";

    public async Task<IReadOnlyList<RawMachineData>> ReadAsync(CancellationToken cancellationToken)
    {
        var targets = _machineCatalogStore.GetTargets()
            .Where(target => string.IsNullOrWhiteSpace(target.Protocol)
                || string.Equals(target.Protocol, "MTConnect", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (targets.Length == 0)
        {
            targets = new[]
            {
                new MachineCatalogTarget(
                    0,
                    _options.MtConnectFallbackMachineCode,
                    _options.MtConnectFallbackMachineIp,
                    BuildFallbackUrl(_options.MtConnectFallbackMachineIp),
                    "MTConnect",
                    null,
                    null,
                    false)
            };
        }

        var results = new List<RawMachineData>();
        foreach (var target in targets)
        {
            try
            {
                using var response = await _httpClient.GetAsync(target.PollUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                results.Add(new RawMachineData(
                    target.MachineIp,
                    Protocol,
                    payload,
                    DateTimeOffset.UtcNow,
                    target.MachineCode,
                    target.PollUrl));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Falha a ler MTConnect para a maquina {MachineIp} ({MachineCode}).",
                    target.MachineIp,
                    target.MachineCode);
            }
        }

        return results;
    }

    private string BuildFallbackUrl(string machineIp)
        => $"http://{machineIp}:{_options.MtConnectPort}{_options.MtConnectCurrentPath}";
}
