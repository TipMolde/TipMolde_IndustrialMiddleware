using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Controllers;

[ApiController]
[Route("api/industrial/sodick")]
public sealed class SodickTelemetryController : ControllerBase
{
    private readonly IMachineTelemetryProcessor _telemetryProcessor;
    private readonly ILogger<SodickTelemetryController> _logger;

    public SodickTelemetryController(
        IMachineTelemetryProcessor telemetryProcessor,
        ILogger<SodickTelemetryController> logger)
    {
        _telemetryProcessor = telemetryProcessor;
        _logger = logger;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> PostTelemetry([FromBody] SodickTelemetryRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MachineIp))
        {
            return BadRequest("MachineIp is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            return BadRequest("Payload is required.");
        }

        var rawData = new RawMachineData(
            request.MachineIp.Trim(),
            string.IsNullOrWhiteSpace(request.Protocol) ? "SODICK" : request.Protocol.Trim(),
            request.Payload,
            request.ReceivedAt ?? DateTimeOffset.UtcNow,
            request.MachineCode,
            request.SourceName);

        _logger.LogInformation(
            "Recebida telemetria Sodick de {MachineIp} via {Protocol}.",
            rawData.MachineIp,
            rawData.Protocol);

        var result = await _telemetryProcessor.ProcessAsync(rawData, cancellationToken);
        return Ok(result);
    }
}
