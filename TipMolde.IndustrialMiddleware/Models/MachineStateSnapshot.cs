namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineStateSnapshot(
    string MachineIp,
    string? State,
    string? Program,
    DateTimeOffset UpdatedAt,
    string? ChangeSignature = null,
    string? RawPayload = null);
