using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    public interface ILanguageTaskQueue
    {
        void QueueTask<TArgs>(TArgs args, Func<TArgs, CancellationToken, ValueTask> task);

        Task<BaseLanguageAction> DequeueAsync(CancellationToken cancellationToken);
    }
}
