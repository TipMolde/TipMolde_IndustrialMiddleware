namespace TipMolde.IndustrialMiddleware.Models;

public sealed record SodickTelemetryRequest(
    string? MachineIp,
    string? Payload,
    string? Protocol,
    string? MachineCode,
    string? SourceName,
    DateTimeOffset? ReceivedAt);
