using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Connectors;

public sealed class OpcUaConnector : IMachineConnector
{
    private readonly IMachineCatalogStore _machineCatalogStore;
    private readonly IOpcUaSimulationStore _simulationStore;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<OpcUaConnector> _logger;

    public OpcUaConnector(
        IMachineCatalogStore machineCatalogStore,
        IOpcUaSimulationStore simulationStore,
        IOptions<IndustrialMiddlewareOptions> options,
        ILogger<OpcUaConnector> logger)
    {
        _machineCatalogStore = machineCatalogStore;
        _simulationStore = simulationStore;
        _options = options.Value;
        _logger = logger;
    }

    public string Protocol => "OPC-UA";

    public Task<IReadOnlyList<RawMachineData>> ReadAsync(CancellationToken cancellationToken)
    {
        var targets = _machineCatalogStore.GetTargets()
            .Where(target => string.Equals(target.Protocol, "OPC-UA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target.Protocol, "OPCUA", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (targets.Length == 0)
        {
            _logger.LogDebug(
                "Sem maquinas OPC-UA no catalogo. O conector fica inativo ate existir uma maquina marcada com esse protocolo.");
            return Task.FromResult<IReadOnlyList<RawMachineData>>(Array.Empty<RawMachineData>());
        }

        if (_options.OpcUaSimulationEnabled)
        {
            return Task.FromResult<IReadOnlyList<RawMachineData>>(BuildSimulationReadings(targets));
        }

        _logger.LogInformation(
            "Foram encontradas {Count} maquinas OPC-UA. O conector esta preparado para ler endpoints, mas o leitor OPC-UA real ainda nao foi ligado.",
            targets.Length);

        return Task.FromResult<IReadOnlyList<RawMachineData>>(Array.Empty<RawMachineData>());
    }

    private IReadOnlyList<RawMachineData> BuildSimulationReadings(MachineCatalogTarget[] targets)
    {
        var results = new List<RawMachineData>();
        var state = _simulationStore.GetState();

        foreach (var target in targets)
        {
            var targetState = state with
            {
                MachineIp = target.MachineIp,
                MachineCode = target.MachineCode,
                EndpointUrl = target.EndpointUrl ?? target.PollUrl ?? state.EndpointUrl
            };

            _logger.LogInformation(
                "OPC-UA simulado -> maquina {MachineIp} ({MachineCode}) | estado {State} | payload {Payload}",
                targetState.MachineIp,
                targetState.MachineCode,
                targetState.State,
                targetState.ToPayload());

            results.Add(new RawMachineData(
                targetState.MachineIp,
                Protocol,
                targetState.ToPayload(),
                DateTimeOffset.UtcNow,
                targetState.MachineCode,
                targetState.EndpointUrl));
        }

        return results;
    }
}
