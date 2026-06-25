using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Controllers;

[ApiController]
[Route("api/industrial/opcua-simulator")]
public sealed class OpcUaSimulatorController : ControllerBase
{
    private readonly IOpcUaSimulationStore _simulationStore;
    private readonly IMachineTelemetryProcessor _telemetryProcessor;
    private readonly ILogger<OpcUaSimulatorController> _logger;

    public OpcUaSimulatorController(
        IOpcUaSimulationStore simulationStore,
        IMachineTelemetryProcessor telemetryProcessor,
        ILogger<OpcUaSimulatorController> logger)
    {
        _simulationStore = simulationStore;
        _telemetryProcessor = telemetryProcessor;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<OpcUaSimulationState> Get()
        => Ok(_simulationStore.GetState());

    [HttpPost]
    public ActionResult<OpcUaSimulationState> Apply([FromBody] OpcUaSimulationCommand command)
        => Ok(_simulationStore.Apply(command));

    [HttpPost("preset/{preset}")]
    public ActionResult<OpcUaSimulationState> ApplyPreset(string preset)
    {
        try
        {
            return Ok(_simulationStore.ApplyPreset(preset));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("reset")]
    public ActionResult<OpcUaSimulationState> Reset()
        => Ok(_simulationStore.Reset());

    [HttpPost("process-now")]
    public async Task<ActionResult<MachineTelemetryProcessingResult>> ProcessNow(CancellationToken cancellationToken)
    {
        var state = _simulationStore.GetState();
        var rawData = new RawMachineData(
            state.MachineIp,
            "OPC-UA",
            state.ToPayload(),
            DateTimeOffset.UtcNow,
            state.MachineCode,
            state.EndpointUrl);

        _logger.LogInformation("Processamento manual OPC-UA simulado pedido pela consola.");
        return Ok(await _telemetryProcessor.ProcessAsync(rawData, cancellationToken));
    }
}
