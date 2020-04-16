using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.LanguageServer.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">command line args.</param>
        /// <returns>Completion.</returns>
        public static async Task Main(string[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Length > 0 && args[0] == "debug")
            {
                Debugger.Launch();
            }

            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .CreateLogger();

            // Configure and launch the server.
            var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .ConfigureLogging(x => x
                        .AddSerilog()
                        .AddLanguageServer()
                        .SetMinimumLevel(LogLevel.Debug))
                    .WithHandler<DocumentSyncHandler>()
                    .WithHandler<DidChangeWatchedFilesHandler>()
                    .WithHandler<TestCompletionHandler>()
                    .WithHandler<TestDefinitionHandler>()
                    .WithHandler<TestHoverHandler>()
                    .WithHandler<InteractionHoverHandler>()
                    .WithHandler<InteractionCompletionHandler>()
                    .WithHandler<AutoStepHandler>()
                    .WithServices(services =>
                    {
                        services.AddSingleton<IWorkspaceHost, WorkspaceHost>();
                        services.AddSingleton<ILanguageTaskQueue, LanguageTaskQueue>();
                        services.AddHostedService<BackgroundLanguageService>();
                    }).OnInitialize(async (s, request) =>
                    {
                        var serviceProvider = s.Services;

                        // Initialise the IHostedService instances.
                        var hostedServices = serviceProvider.GetServices<IHostedService>();

                        foreach (var srv in hostedServices)
                        {
                            await srv.StartAsync(CancellationToken.None);
                        }

                        var projectHost = serviceProvider.GetService<IWorkspaceHost>();

                        // Init the project host with the root folder.
                        projectHost.Initialise(request.RootUri);
                    }));

            // Wait until it closes.
            await server.WaitForExit;

            // Stop any background operations.
            var background = server.Services.GetServices<IHostedService>();

            foreach (var hosted in background)
            {
                await hosted.StopAsync(CancellationToken.None);
            }
        }
    }
}
