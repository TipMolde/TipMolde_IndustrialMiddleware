namespace TipMolde.IndustrialMiddleware.Models;

public sealed record ProtocolDetectionResult(
    string MachineIp,
    bool Detected,
    string? Protocol,
    string? EndpointUrl,
    string? Message);
