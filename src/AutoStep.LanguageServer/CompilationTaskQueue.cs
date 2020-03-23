using AutoStep.Projects;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public class CompileTask
    {
        public CompileTask(IProjectHost projectHost)
        {
            ProjectHost = projectHost;
        }

        public IProjectHost ProjectHost { get; }
    }

    public class CompileProjectTask : CompileTask
    {
        public CompileProjectTask(IProjectHost projectHost) : base(projectHost)
        {
        }
    }

    public class GenerateFeatureListTask : CompileTask
    {
        public GenerateFeatureListTask(IProjectHost projectHost) : base(projectHost)
        {
        }
    }

    public interface ICompilationTaskQueue
    {
        void QueueCompileTask(CompileTask task);

        Task<CompileTask> DequeueAsync(CancellationToken cancellationToken);
    }

    public class CompilationTaskQueue : ICompilationTaskQueue
    {
        private ConcurrentQueue<CompileTask> taskQueue = new ConcurrentQueue<CompileTask>();
        private SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueCompileTask(CompileTask workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            taskQueue.Enqueue(workItem);
            signal.Release();
        }

        public async Task<CompileTask> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            taskQueue.TryDequeue(out var workItem);

            return workItem;
        }
    }
}
