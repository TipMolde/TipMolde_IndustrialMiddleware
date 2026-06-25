using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipMolde.IndustrialMiddleware.Interfaces;
using TipMolde.IndustrialMiddleware.Clients;
using TipMolde.IndustrialMiddleware.Connectors;
using TipMolde.IndustrialMiddleware.Options;
using TipMolde.IndustrialMiddleware.Services;
using TipMolde.IndustrialMiddleware.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<IndustrialMiddlewareOptions>(
    builder.Configuration.GetSection(IndustrialMiddlewareOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SodickIngress", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

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
builder.Services.AddSingleton<IMachineStateStore, InMemoryMachineStateStore>();
builder.Services.AddSingleton<IMachineCatalogStore, InMemoryMachineCatalogStore>();
builder.Services.AddSingleton<IMachineTelemetryProcessor, MachineTelemetryProcessor>();
builder.Services.AddSingleton<IOpcUaSimulationStore, InMemoryOpcUaSimulationStore>();

builder.Services.AddSingleton<IMachineConnector, OpcUaConnector>();
builder.Services.AddHttpClient<MtConnectConnector>();
builder.Services.AddSingleton<IMachineConnector>(sp => sp.GetRequiredService<MtConnectConnector>());
builder.Services.AddHttpClient<SodickHttpConnector>();
builder.Services.AddSingleton<IMachineConnector>(sp => sp.GetRequiredService<SodickHttpConnector>());

builder.Services.AddHostedService<MachineCatalogSyncWorker>();
builder.Services.AddHostedService<MachinePollingWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("SodickIngress");
app.MapControllers();

await app.RunAsync();
