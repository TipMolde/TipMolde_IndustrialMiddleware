namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineTelemetryDto(
    string MachineIp,
    string Protocol,
    DateTimeOffset OccurredAt,
    string State,
    string? Program,
    int? PieceCounter,
    IReadOnlyList<string> AlarmCodes,
    string? OperatorCode,
    string? PartCode,
    string? MoldCode,
    string? SourceName,
    string RawPayload);
