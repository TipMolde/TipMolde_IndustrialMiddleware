namespace TipMolde.IndustrialMiddleware.Models;

public sealed record RawMachineData(
    string MachineIp,
    string Protocol,
    string Payload,
    DateTimeOffset ReceivedAt,
    string? MachineCode = null,
    string? SourceName = null);
