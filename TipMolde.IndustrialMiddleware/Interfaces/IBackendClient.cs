using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Interfaces;

public interface IBackendClient
{
    Task SendTelemetryAsync(IEnumerable<MachineTelemetryDto> telemetry, CancellationToken cancellationToken);
}
