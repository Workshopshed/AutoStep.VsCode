using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    /// <summary>
    /// Interface for the language task queue.
    /// </summary>
    public interface ILanguageTaskQueue
    {
        /// <summary>
        /// Queue a task.
        /// </summary>
        /// <typeparam name="TArgs">The argument structure type.</typeparam>
        /// <param name="args">The arguments to pass to the method when invoked.</param>
        /// <param name="task">The async callback to call.</param>
        void QueueTask<TArgs>(TArgs args, Func<TArgs, CancellationToken, ValueTask> task);

        /// <summary>
        /// Invoked to retrieve the next action in the queue.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The next language action.</returns>
        Task<BaseLanguageAction?> DequeueAsync(CancellationToken cancellationToken);
    }
}
