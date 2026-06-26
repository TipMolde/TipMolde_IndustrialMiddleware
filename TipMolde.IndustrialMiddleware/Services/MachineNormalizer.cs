using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using System.Text.RegularExpressions;

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
        var normalizedPayload = NormalizePayload(rawData.Payload);
        var context = _contextParser.Parse(normalizedPayload);
        var state = ExtractState(normalizedPayload);
        var program = ExtractValue(normalizedPayload, "PROGRAM") ?? rawData.MachineCode;
        var alarms = ExtractValues(normalizedPayload, "ALARM");
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
            ExtractInt(normalizedPayload, "COUNTER"),
            alarms,
            context,
            integrationLevel,
            confidence,
            completeness,
            rawData.ReceivedAt,
            rawData.Payload);
    }

    private static string ExtractState(string payload)
        => ExtractValue(payload, "STATE")
           ?? ExtractValue(payload, "EXECUTION")
           ?? ExtractKeywordState(payload)
           ?? "UNKNOWN";

    private static string? ExtractValue(string payload, string key)
    {
        var candidatePatterns = new[]
        {
            $@"\b{Regex.Escape(key)}\b\s*[:=]\s*(?<value>[^\r\n;<]+)",
            $@"\b{Regex.Escape(key)}\b\s+(?<value>[^\r\n;<]+)"
        };

        foreach (var pattern in candidatePatterns)
        {
            var match = Regex.Match(payload, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return StripHtml(value);
                }
            }
        }

        var tokens = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return StripHtml(parts[1].Trim());
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

    private static string NormalizePayload(string payload)
        => StripHtml(payload).Replace("\r", " ").Replace("\n", " ");

    private static string StripHtml(string text)
        => Regex.Replace(text, "<[^>]+>", " ", RegexOptions.CultureInvariant | RegexOptions.Singleline).Trim();

    private static string? ExtractKeywordState(string payload)
    {
        var keywords = new[]
        {
            "RUNNING",
            "ACTIVE",
            "EXECUTING",
            "STOPPED",
            "IDLE",
            "PAUSED",
            "ALARM",
            "FAULT"
        };

        foreach (var keyword in keywords)
        {
            if (Regex.IsMatch(payload, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return keyword;
            }
        }

        return null;
    }

    private static bool IsContextComplete(MachineContext context)
        => !string.IsNullOrWhiteSpace(context.OperatorCode)
           && !string.IsNullOrWhiteSpace(context.PartCode)
           && !string.IsNullOrWhiteSpace(context.MoldCode);
}
