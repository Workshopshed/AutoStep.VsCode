using System;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.LanguageServer.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Provides a background service for executing queued language tasks.
    /// </summary>
    public class BackgroundLanguageService : BackgroundService
    {
        private readonly ILanguageTaskQueue taskQueue;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundLanguageService"/> class.
        /// </summary>
        /// <param name="taskQueue">The shared queue.</param>
        /// <param name="logger">The logger.</param>
        public BackgroundLanguageService(ILanguageTaskQueue taskQueue, ILogger<BackgroundLanguageService> logger)
        {
            this.taskQueue = taskQueue;
            this.logger = logger;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await BackgroundProcessing(stoppingToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Need to log any serious issues, but then continue.")]
        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dequeue an item and execute it.
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                if (workItem is object)
                {
                    try
                    {
                        await workItem.Execute(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, TaskMessages.WorkItemError, ex.Message);
                    }
                }
            }
        }
    }
}
