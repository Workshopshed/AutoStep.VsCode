using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    /// <summary>
    /// Base class for some action that can execute.
    /// </summary>
    public abstract class BaseLanguageAction
    {
        /// <summary>
        /// Execute the language action.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token (to stop the action).</param>
        /// <returns>Awaitable task.</returns>
        public abstract ValueTask Execute(CancellationToken cancellationToken);
    }
}
