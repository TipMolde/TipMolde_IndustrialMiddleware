using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Models;
using TipMolde.IndustrialMiddleware.Options;

namespace TipMolde.IndustrialMiddleware.Clients;

public sealed class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly IndustrialMiddlewareOptions _options;
    private readonly ILogger<BackendClient> _logger;

    public BackendClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<IndustrialMiddlewareOptions> options,
        ILogger<BackendClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEventsAsync(IEnumerable<MachineEventDto> events, CancellationToken cancellationToken)
    {
        var payload = events.ToArray();
        if (payload.Length == 0)
        {
            return;
        }

        var outgoingJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogInformation(
            "Payload JSON que o middleware vai enviar para o backend:{NewLine}{Json}",
            Environment.NewLine,
            outgoingJson);

        using var response = await _httpClient.PostAsJsonAsync(_options.BackendEventsPath, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
