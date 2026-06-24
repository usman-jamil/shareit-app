using Application;
using Cli.Commands;
using ConsoleAppFramework;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddLogging(x => x.AddSerilog());
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
using IHost host = builder.Build();

ConsoleApp.ServiceProvider = host.Services;
ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();

consoleApp.Add<DatabaseCommands>();

await consoleApp.RunAsync(args);
