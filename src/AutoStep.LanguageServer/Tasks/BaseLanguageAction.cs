using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer.Tasks
{
    public abstract class BaseLanguageAction
    {
        public abstract ValueTask Execute(CancellationToken cancellationToken);
    }
}
