using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    /// <summary>
    /// Provides a queue of tasks that can be queued and de-queued concurrently.
    /// </summary>
    public sealed class LanguageTaskQueue : ILanguageTaskQueue, IDisposable
    {
        private ConcurrentQueue<BaseLanguageAction> taskQueue = new ConcurrentQueue<BaseLanguageAction>();
        private SemaphoreSlim signal = new SemaphoreSlim(0);

        /// <inheritdoc/>
        public async Task<BaseLanguageAction?> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            taskQueue.TryDequeue(out var workItem);

            return workItem;
        }

        /// <inheritdoc/>
        public void QueueTask<TArgs>(TArgs args, Func<TArgs, CancellationToken, ValueTask> task)
        {
            QueueTask(new LanguageAction<TArgs>(args, task));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            signal.Dispose();
        }

        private void QueueTask(BaseLanguageAction workItem)
        {
            taskQueue.Enqueue(workItem);
            signal.Release();
        }
    }
}
