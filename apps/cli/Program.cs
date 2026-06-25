using Application;
using Cli.Commands;
using ConsoleAppFramework;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

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
consoleApp.Add<UserCommands>();
consoleApp.Add<ApiKeyCommands>();

await consoleApp.RunAsync(args);
