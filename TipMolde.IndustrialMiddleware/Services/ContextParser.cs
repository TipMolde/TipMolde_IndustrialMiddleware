using System.Text.RegularExpressions;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class ContextParser : IContextParser
{
    private static readonly Regex PairRegex = new(
        @"(?<key>[A-Za-z_]+)\s*=\s*(?<value>[^;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public MachineContext? Parse(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in PairRegex.Matches(rawMessage))
        {
            values[match.Groups["key"].Value.Trim()] = match.Groups["value"].Value.Trim();
        }

        if (values.Count == 0)
        {
            return null;
        }

        values.TryGetValue("OP", out var operatorCode);
        values.TryGetValue("OF", out var workOrderCode);
        values.TryGetValue("OPERACAO", out var operationCode);
        values.TryGetValue("PECA", out var partCode);
        values.TryGetValue("MOLDE", out var moldCode);

        return new MachineContext(operatorCode, workOrderCode, operationCode, partCode, moldCode);
    }
}
