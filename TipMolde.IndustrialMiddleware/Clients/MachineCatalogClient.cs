using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Clients;

public sealed class MachineCatalogClient : IMachineCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<MachineCatalogClient> _logger;

    public MachineCatalogClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<IndustrialMiddlewareOptions> options,
        ILogger<MachineCatalogClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BackendBearerToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BackendBearerToken);
        }
    }

    public async Task<IReadOnlyList<MachineCatalogTarget>> GetMachineTargetsAsync(CancellationToken cancellationToken)
    {
        var results = new List<MachineCatalogTarget>();
        var page = 1;

        while (true)
        {
            var uri = $"{_options.BackendMachinesPath}?page={page}&pageSize={_options.MachineCatalogPageSize}";
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var items = ReadItems(root);
            if (items.Count == 0)
            {
                break;
            }

            results.AddRange(items);

            if (ShouldStopPaging(root, items.Count, page, _options.MachineCatalogPageSize))
            {
                break;
            }

            page++;
        }

        return results;
    }

    private IReadOnlyList<MachineCatalogTarget> ReadItems(JsonElement root)
    {
        var itemsElement = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("items", out var items) ? items : default;

        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MachineCatalogTarget>();
        }

        var results = new List<MachineCatalogTarget>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            if (TryReadTarget(item, out var target))
            {
                results.Add(target);
            }
        }

        return results;
    }

    private bool TryReadTarget(JsonElement item, out MachineCatalogTarget target)
    {
        target = default!;

        var ip = ReadString(item, "ipAddress", "IpAddress", "ip", "machineIp");
        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        var state = ReadString(item, "estado", "Estado", "state", "State");
        var isMaintenance = string.Equals(state, "MANUTENCAO", StringComparison.OrdinalIgnoreCase);
        if (_options.IgnoreMachinesInMaintenance && isMaintenance)
        {
            return false;
        }

        var machineId = ReadInt(item, "maquina_id", "Machine_id", "machineId", "id") ?? 0;
        var machineCode = ReadString(item, "numero", "Numero", "code", "Code")
            ?? ReadString(item, "nomeModelo", "NomeModelo", "name", "Name")
            ?? $"M{machineId}";
        var protocol = ReadString(
            item,
            "protocoloComunicacao",
            "ProtocoloComunicacao",
            "protocol",
            "Protocol",
            "communicationProtocol",
            "CommunicationProtocol");
        var endpointUrl = ReadString(item, "endpointUrl", "EndpointUrl", "opcUaEndpointUrl", "OpcUaEndpointUrl");

        var pollUrl = BuildPollUrl(ip, protocol);
        if (string.IsNullOrWhiteSpace(endpointUrl) && string.Equals(protocol, "OPC-UA", StringComparison.OrdinalIgnoreCase))
        {
            endpointUrl = $"opc.tcp://{ip}:4840";
        }
        else if (string.IsNullOrWhiteSpace(endpointUrl) && IsSodickProtocol(protocol))
        {
            endpointUrl = $"http://{ip}";
        }

        target = new MachineCatalogTarget(
            machineId,
            machineCode,
            ip,
            pollUrl,
            protocol,
            endpointUrl,
            state,
            isMaintenance);
        return true;
    }

    private string BuildPollUrl(string ip, string? protocol)
    {
        if (IsSodickProtocol(protocol))
        {
            return $"http://{ip}";
        }

        if (string.Equals(protocol, "OPC-UA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, "OPCUA", StringComparison.OrdinalIgnoreCase))
        {
            return $"opc.tcp://{ip}:4840";
        }

        return $"http://{ip}:{_options.MtConnectPort}{_options.MtConnectCurrentPath}";
    }

    private static bool IsSodickProtocol(string? protocol)
        => string.Equals(protocol, "SODICK", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "SODICK-HTTP", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "SODICK_HTTP", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldStopPaging(JsonElement root, int itemsCount, int currentPage, int pageSize)
    {
        if (root.TryGetProperty("totalCount", out var totalCountElement) && totalCountElement.TryGetInt32(out var totalCount))
        {
            return currentPage * pageSize >= totalCount;
        }

        if (root.TryGetProperty("currentPage", out var currentPageElement)
            && root.TryGetProperty("pageSize", out var pageSizeElement)
            && currentPageElement.TryGetInt32(out var apiCurrentPage)
            && pageSizeElement.TryGetInt32(out var apiPageSize))
        {
            return apiCurrentPage * apiPageSize >= itemsCount;
        }

        return itemsCount < pageSize;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
                {
                    return value;
                }

                if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }
}
