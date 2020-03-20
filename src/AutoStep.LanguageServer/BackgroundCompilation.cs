using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public class BackgroundCompilation : BackgroundService
    {
        private readonly ICompilationTaskQueue taskQueue;
        private readonly IProjectHost projectHost;
        private readonly ILoggerFactory logFactory;
        private readonly ILogger<BackgroundCompilation> logger;

        public BackgroundCompilation(ICompilationTaskQueue taskQueue, IProjectHost projectHost, ILoggerFactory logFactory)
        {
            this.taskQueue = taskQueue;
            this.projectHost = projectHost;
            this.logFactory = logFactory;
            this.logger = logFactory.CreateLogger<BackgroundCompilation>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Background Compilation Started");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task HandleCompileTask(CompileTask task, CancellationToken stopToken)
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
                    await HandleCompileTask(workItem, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }
    }
}
