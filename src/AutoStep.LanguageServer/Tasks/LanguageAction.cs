using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    public class LanguageAction<T> : BaseLanguageAction
    {
        private readonly T args;
        private readonly Func<T, CancellationToken, ValueTask> action;

        public LanguageAction(T args, Func<T, CancellationToken, ValueTask> action)
        {
            this.args = args;
            this.action = action;
        }

        public override async ValueTask Execute(CancellationToken cancelToken)
        {
            await action(args, cancelToken);
        }
    }
}
