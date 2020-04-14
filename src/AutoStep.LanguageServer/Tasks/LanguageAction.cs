using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    /// <summary>
    /// Represents a typed callback action.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    public class LanguageAction<T> : BaseLanguageAction
    {
        private readonly T args;
        private readonly Func<T, CancellationToken, ValueTask> action;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageAction{T}"/> class.
        /// </summary>
        /// <param name="args">The argument.</param>
        /// <param name="action">The callback.</param>
        public LanguageAction(T args, Func<T, CancellationToken, ValueTask> action)
        {
            this.args = args;
            this.action = action;
        }

        /// <inheritdoc/>
        public override async ValueTask Execute(CancellationToken cancelToken)
        {
            await action(args, cancelToken);
        }
    }
}
