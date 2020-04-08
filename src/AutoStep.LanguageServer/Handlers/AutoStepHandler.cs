using AutoStep.Elements.Metadata;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using AutoStep.Projects;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public class FeatureRequest : IRequest<FeatureSetParams>
    {

    }

    public class FeatureInfo
    {
        public string SourceFile { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }
    }

    public class FeatureSetParams
    {
        public IEnumerable<FeatureInfo> Features { get; set; }
    }

    [Method("autostep/features")]
    public interface IAutoStepFeatureRequestHandler : IRequestHandler<FeatureRequest, FeatureSetParams>, IJsonRpcRequestHandler<FeatureRequest, FeatureSetParams>
    {
    }

    public class AutoStepHandler : IAutoStepFeatureRequestHandler
    {
        private readonly IProjectHost host;
        private readonly ILoggerFactory logFactory;

        public AutoStepHandler(IProjectHost host, ILoggerFactory logFactory)
        {
            this.host = host;
            this.logFactory = logFactory;
        }

        public Task<FeatureSetParams> Handle(FeatureRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FeatureSetParams
            {
                Features = GetFeatureInfo()
            });
        }

        private IEnumerable<FeatureInfo> GetFeatureInfo()
        {
            // Query the set of all known features.
            foreach (var file in host.ProjectContext.Project.AllFiles.Values.OfType<ProjectTestFile>())
            {
                if(file.LastCompileResult?.Output is FileElement built)
                {
                    if(!string.IsNullOrEmpty(built.Feature?.Name))
                    {
                        yield return new FeatureInfo
                        {
                            SourceFile = file.Path,
                            Name = built.Feature.Name,
                            Description = built.Feature.Description
                        };
                    }
                }
            }
        }
    }
}
