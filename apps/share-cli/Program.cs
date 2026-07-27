using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Share.Application;
using Share.Cli.Commands;
using Share.Infrastructure;
using Share.Infrastructure.Configuration;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

// Added last on purpose: the user's ~/.share/config.yaml is the source of truth and
// overrides appsettings.json, user secrets and environment variables.
builder.Configuration.AddShareCliConfigurationFile();

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddLogging(x => x.AddSerilog());
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
using IHost host = builder.Build();

ConsoleApp.ServiceProvider = host.Services;
ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();

consoleApp.Add<PingCommands>();
consoleApp.Add<ConfigCommands>("config");

await consoleApp.RunAsync(args);
