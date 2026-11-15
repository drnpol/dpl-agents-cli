using System;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace DPL.Agents.Cli;

public class Program {
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSerilog(configuration =>
        {
            var outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} - {Level:u4} - {Message:lj}{Exception}{NewLine}";
            configuration
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                $".\\logs\\application-.log",
                outputTemplate: outputTemplate,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true
            );
        });
        builder.Services.AddSingleton<Application>();
        
        var host = builder.Build();

        var application = host.Services.GetRequiredService<Application>();

        application.Run();
        
    }
}