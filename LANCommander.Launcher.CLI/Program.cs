﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Serilog;

var settings = SettingService.GetSettings();

using var logger = new LoggerConfiguration()
    .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(settings.Debug.LoggingPath, "log-.txt"), rollingInterval: settings.Debug.LoggingArchivePeriod)
#if DEBUG
    .WriteTo.Seq("http://localhost:5341")
#endif
    .CreateLogger();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
    loggingBuilder.AddSerilog(logger);
});

builder.Services.AddLANCommander();

using IHost host = builder.Build();

host.Services.InitializeLANCommander();

using var scope = host.Services.CreateScope();

var commandLineService = scope.ServiceProvider.GetService<CommandLineService>();

await commandLineService.ParseCommandLineAsync(args);