using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IMachineTelemetryProcessor
{
    Task<MachineTelemetryProcessingResult> ProcessAsync(RawMachineData rawData, CancellationToken cancellationToken);
}
