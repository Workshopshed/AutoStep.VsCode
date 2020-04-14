using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Elements.Test;
using AutoStep.Projects;
using Microsoft.Extensions.Logging;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Custom JSON-RPC handler for autostep requests (i.e. extensions to the language server protocol specific to autostep functionality).
    /// </summary>
    public class AutoStepHandler : IAutoStepFeatureRequestHandler
    {
        private readonly IWorkspaceHost host;
        private readonly ILoggerFactory logFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoStepHandler"/> class.
        /// </summary>
        /// <param name="host">The workspace host.</param>
        /// <param name="logFactory">The log factory.</param>
        public AutoStepHandler(IWorkspaceHost host, ILoggerFactory logFactory)
        {
            this.host = host;
            this.logFactory = logFactory;
        }

        /// <inheritdoc/>
        public Task<FeatureSetResult> Handle(FeatureRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FeatureSetResult(GetFeatureInfo()));
        }

        private IEnumerable<FeatureInfo> GetFeatureInfo()
        {
            // Query the set of all known features.
            foreach (var file in host.GetProjectFilesOfType<ProjectTestFile>())
            {
                if (file.LastCompileResult?.Output is FileElement built)
                {
                    if (!string.IsNullOrEmpty(built.Feature?.Name))
                    {
                        yield return new FeatureInfo(file.Path, built.Feature.Name, built.Feature.Description);
                    }
                }
            }
        }
    }
}
