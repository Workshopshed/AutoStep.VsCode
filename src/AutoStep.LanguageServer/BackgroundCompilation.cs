using AutoStep.Execution;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public class BackgroundCompilation : BackgroundService
    {
        private readonly ICompilationTaskQueue taskQueue;
        private readonly IProjectHost projectHost;
        private readonly ILanguageServer server;
        private readonly ILoggerFactory logFactory;
        private readonly ILogger<BackgroundCompilation> logger;

        public BackgroundCompilation(ICompilationTaskQueue taskQueue, IProjectHost projectHost, ILanguageServer server, ILoggerFactory logFactory)
        {
            this.taskQueue = taskQueue;
            this.projectHost = projectHost;
            this.server = server;
            this.logFactory = logFactory;
            this.logger = logFactory.CreateLogger<BackgroundCompilation>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Background Compilation Started");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task HandleCompileTask(CompileProjectTask task, CancellationToken stopToken)
        {
            logger.LogDebug("Starting Compile");

            var builder = task.ProjectHost.Project.Compiler;

            await builder.CompileAsync(logFactory, stopToken);

            if (!stopToken.IsCancellationRequested)
            {
                builder.Link(stopToken);
            }

            if(!stopToken.IsCancellationRequested)
            {
                projectHost.OnProjectCompiled();
            }
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    await (workItem switch
                    {
                        CompileProjectTask cp => HandleCompileTask(cp, stoppingToken),
                        _ => throw new InvalidOperationException()
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }
    }
}
