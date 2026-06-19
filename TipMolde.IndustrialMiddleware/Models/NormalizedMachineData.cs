namespace TipMolde.IndustrialMiddleware.Models;

public sealed record NormalizedMachineData(
    string MachineIp,
    string Protocol,
    string State,
    string? Program,
    int? PieceCounter,
    IReadOnlyList<string> AlarmCodes,
    MachineContext? Context,
    IntegrationLevel IntegrationLevel,
    double Confidence,
    string ContextCompleteness,
    DateTimeOffset OccurredAt,
    string RawPayload);
