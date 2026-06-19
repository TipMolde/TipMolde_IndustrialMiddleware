namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineCatalogTarget(
    int MachineId,
    string MachineCode,
    string MachineIp,
    string PollUrl,
    string? Protocol,
    string? EndpointUrl,
    string? State,
    bool IsInMaintenance);
