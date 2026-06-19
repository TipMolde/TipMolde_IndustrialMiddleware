namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineEventDto(
    string MachineIp,
    string EventType,
    DateTimeOffset OccurredAt,
    string? WorkOrderCode,
    string? OperationCode,
    string? PartCode,
    string? MoldCode,
    string? Program,
    double Confidence,
    string ContextCompleteness,
    string CorrelationId);
