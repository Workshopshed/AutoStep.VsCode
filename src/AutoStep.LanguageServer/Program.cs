using AutoStep.LanguageServer.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    // Declare a 'project host' service, that contains the top-level state.
    // On initialisation we start with the disk contents, and then, when a file is opened,
    // we can override the file content with local content.
    // This is going to be similar to the Monaco content source.

    class Program
    {
        static async Task Main(string[] args)
        {
            if(args.Length > 0 && args[0] == "debug")
            {
                Debugger.Launch();
            }

            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
              .CreateLogger();

            Log.Logger.Information("This only goes file...");

            var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .ConfigureLogging(x => x
                        .AddSerilog()
                        .AddLanguageServer()
                        .SetMinimumLevel(LogLevel.Debug))
                    .WithHandler<TextDocumentHandler>()
                    .WithHandler<DidChangeWatchedFilesHandler>()
                    .WithHandler<TestCompletionHandler>()
                    .WithHandler<TestDefinitionHandler>()
                    .WithHandler<TestHoverHandler>()
                    .WithHandler<AutoStepHandler>()
                    .WithServices(services => {

                        services.AddSingleton<IProjectHost, ProjectHost>();
                        services.AddSingleton<ILanguageTaskQueue, LanguageTaskQueue>();
                        services.AddHostedService<BackgroundLanguageService>();

                    }).OnInitialize(async (s, request) => {

                        // Initialize cancel source.
                        var serviceProvider = s.Services;

                        // Initialise the IHostedService instances.
                        var hostedServices = serviceProvider.GetServices<IHostedService>();

                        foreach(var srv in hostedServices)
                        {
                            await srv.StartAsync(CancellationToken.None);
                        }

                        var projectHost = serviceProvider.GetService<IProjectHost>();

                        // Init the project host with the root folder.
                        projectHost.Initialize(request.RootUri);
                    })
                );

            await server.WaitForExit;

            var background = server.Services.GetServices<IHostedService>();

            foreach (var hosted in background)
            {
                await hosted.StopAsync(CancellationToken.None);
            }
        }
    }
}
