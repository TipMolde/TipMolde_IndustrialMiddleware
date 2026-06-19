using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Clients;
using TipMolde.IndustrialMiddleware.Connectors;
using TipMolde.IndustrialMiddleware.Options;
using TipMolde.IndustrialMiddleware.Services;
using TipMolde.IndustrialMiddleware.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<IndustrialMiddlewareOptions>(
    builder.Configuration.GetSection(IndustrialMiddlewareOptions.SectionName));

builder.Services.AddHttpClient<IBackendClient, BackendClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<IndustrialMiddlewareOptions>>().Value;
    client.BaseAddress = new Uri(options.BackendBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.BackendRequestTimeoutSeconds);
});

builder.Services.AddHttpClient<IMachineCatalogClient, MachineCatalogClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<IndustrialMiddlewareOptions>>().Value;
    client.BaseAddress = new Uri(options.BackendBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.BackendRequestTimeoutSeconds);
});

builder.Services.AddSingleton<IContextParser, ContextParser>();
builder.Services.AddSingleton<IMtConnectSnapshotParser, MtConnectSnapshotParser>();
builder.Services.AddSingleton<IMachineNormalizer, MachineNormalizer>();
builder.Services.AddSingleton<IEventDetector, MachineEventDetector>();
builder.Services.AddSingleton<IMachineStateStore, InMemoryMachineStateStore>();
builder.Services.AddSingleton<IMachineCatalogStore, InMemoryMachineCatalogStore>();

builder.Services.AddSingleton<IMachineConnector, OpcUaConnector>();
builder.Services.AddHttpClient<MtConnectConnector>();
builder.Services.AddSingleton<IMachineConnector>(sp => sp.GetRequiredService<MtConnectConnector>());

builder.Services.AddHostedService<MachineCatalogSyncWorker>();
builder.Services.AddHostedService<MachinePollingWorker>();

var host = builder.Build();
await host.RunAsync();
