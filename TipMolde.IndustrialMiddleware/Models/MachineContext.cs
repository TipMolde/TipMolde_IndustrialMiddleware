namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineContext(
    string? OperatorCode,
    string? PartCode,
    string? MoldCode);
