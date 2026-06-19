namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineContext(
    string? OperatorCode,
    string? WorkOrderCode,
    string? OperationCode,
    string? PartCode,
    string? MoldCode);
