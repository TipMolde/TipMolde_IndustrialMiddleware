namespace TipMolde.IndustrialMiddleware.Models;

public sealed record MachineTelemetryProcessingResult(
    string MachineIp,
    string Protocol,
    bool HasChanged,
    int RecordsPrepared,
    bool SentToBackend,
    DateTimeOffset ProcessedAt);
