using AutoStep.Execution;
using AutoStep.LanguageServer.Tasks;
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
    public class BackgroundLanguageService : BackgroundService
    {
        private readonly ILanguageTaskQueue taskQueue;
        private readonly ILogger logger;

        public BackgroundLanguageService(ILanguageTaskQueue taskQueue, ILogger<BackgroundLanguageService> logger)
        {
            this.taskQueue = taskQueue;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    await workItem.Execute(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }
    }
}
