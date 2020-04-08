using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    public class LanguageTaskQueue : ILanguageTaskQueue
    {
        private ConcurrentQueue<BaseLanguageAction> taskQueue = new ConcurrentQueue<BaseLanguageAction>();
        private SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueTask(BaseLanguageAction workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            taskQueue.Enqueue(workItem);
            signal.Release();
        }

        public async Task<BaseLanguageAction> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            taskQueue.TryDequeue(out var workItem);

            return workItem;
        }

        public void QueueTask<TArgs>(TArgs args, Func<TArgs, CancellationToken, ValueTask> task)
        {
            QueueTask(new LanguageAction<TArgs>(args, task));
        }
    }
}
