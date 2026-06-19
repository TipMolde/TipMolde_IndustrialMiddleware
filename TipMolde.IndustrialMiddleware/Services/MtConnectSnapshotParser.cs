using System.Text.RegularExpressions;
using System.Xml.Linq;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;

namespace TipMolde.IndustrialMiddleware.Services;

public sealed class MtConnectSnapshotParser : IMtConnectSnapshotParser
{
    private static readonly Regex XmlDeclarationRegex = new(
        @"^\s*<\?xml[^>]*\?>\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public MtConnectSnapshot Parse(string machineIp, string xmlPayload, DateTimeOffset receivedAt)
    {
        var cleanedPayload = CleanPayload(xmlPayload);
        var root = XElement.Parse($"<Root>{cleanedPayload}</Root>");

        var execution = FindValue(root, "Execution", "RunStatus", "rstat");
        var program = FindValue(root, "Program", "Program", "ncprog");
        var activeAlarms = FindValue(root, "Message", "ActiveAlarms", "aalarms");
        var m30c1 = FindInt(root, "Message", "M30Counter1", "m30c1");
        var m30c2 = FindInt(root, "Message", "M30Counter2", "m30c2");
        var loopsRemaining = FindInt(root, "Message", "LoopsRemaining", "lpremain");
        var eventLogEntries = FindEventLogEntries(root);

        return new MtConnectSnapshot(
            machineIp,
            execution,
            program,
            string.IsNullOrWhiteSpace(activeAlarms) ? null : activeAlarms,
            m30c1,
            m30c2,
            loopsRemaining,
            eventLogEntries,
            receivedAt);
    }

    private static string CleanPayload(string xmlPayload)
    {
        if (string.IsNullOrWhiteSpace(xmlPayload))
        {
            return string.Empty;
        }

        return XmlDeclarationRegex.Replace(xmlPayload.Trim(), string.Empty);
    }

    private static string? FindValue(XElement root, string elementName, string? nameAttribute = null, string? dataItemId = null)
    {
        foreach (var element in root.Descendants().Where(x => x.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase)))
        {
            if (Matches(element, nameAttribute, dataItemId))
            {
                var value = element.Value.Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private static int? FindInt(XElement root, string elementName, string? nameAttribute = null, string? dataItemId = null)
    {
        var value = FindValue(root, elementName, nameAttribute, dataItemId);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> FindEventLogEntries(XElement root)
    {
        return root
            .Descendants()
            .Where(x => x.Name.LocalName.Equals("EventLogEntry", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(5)
            .ToArray();
    }

    private static bool Matches(XElement element, string? nameAttribute, string? dataItemId)
    {
        var name = element.Attribute("name")?.Value;
        var id = element.Attribute("dataItemId")?.Value;

        var nameMatches = string.IsNullOrWhiteSpace(nameAttribute)
            || string.Equals(name, nameAttribute, StringComparison.OrdinalIgnoreCase);

        var idMatches = string.IsNullOrWhiteSpace(dataItemId)
            || string.Equals(id, dataItemId, StringComparison.OrdinalIgnoreCase);

        return nameMatches && idMatches;
    }
}
