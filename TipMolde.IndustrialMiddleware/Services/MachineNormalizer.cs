using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class MachineNormalizer : IMachineNormalizer
{
    private readonly IContextParser _contextParser;

    public MachineNormalizer(IContextParser contextParser)
    {
        _contextParser = contextParser;
    }

    public NormalizedMachineData Normalize(RawMachineData rawData)
    {
        var context = _contextParser.Parse(rawData.Payload);
        var state = ExtractState(rawData.Payload);
        var program = ExtractValue(rawData.Payload, "PROGRAM") ?? rawData.MachineCode;
        var alarms = ExtractValues(rawData.Payload, "ALARM");
        var integrationLevel = context is null
            ? (string.IsNullOrWhiteSpace(program) ? IntegrationLevel.Basic : IntegrationLevel.Basic)
            : IntegrationLevel.Partial;

        var confidence = context is null ? 0.55 : 0.85;
        var completeness = context is null
            ? "BASIC"
            : IsContextComplete(context) ? "FULL" : "PARTIAL";

        return new NormalizedMachineData(
            rawData.MachineIp,
            rawData.Protocol,
            state,
            program,
            ExtractInt(rawData.Payload, "COUNTER"),
            alarms,
            context,
            integrationLevel,
            confidence,
            completeness,
            rawData.ReceivedAt,
            rawData.Payload);
    }

    private static string ExtractState(string payload)
        => ExtractValue(payload, "STATE") ?? ExtractValue(payload, "EXECUTION") ?? "UNKNOWN";

    private static string? ExtractValue(string payload, string key)
    {
        var tokens = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractValues(string payload, string key)
    {
        var value = ExtractValue(payload, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int? ExtractInt(string payload, string key)
        => int.TryParse(ExtractValue(payload, key), out var value) ? value : null;

    private static bool IsContextComplete(MachineContext context)
        => !string.IsNullOrWhiteSpace(context.OperatorCode)
           && !string.IsNullOrWhiteSpace(context.WorkOrderCode)
           && !string.IsNullOrWhiteSpace(context.OperationCode)
           && !string.IsNullOrWhiteSpace(context.PartCode)
           && !string.IsNullOrWhiteSpace(context.MoldCode);
}
