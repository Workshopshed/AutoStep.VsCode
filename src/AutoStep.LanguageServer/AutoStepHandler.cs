using AutoStep.Execution;
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

    public class FeatureSetParams
    {
        public IEnumerable<string> FeatureNames { get; set; }
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
            var featureSet = FeatureExecutionSet.Create(host.Project, new RunAllFilter(), logFactory);

            return Task.FromResult(new FeatureSetParams
            {
                FeatureNames = featureSet.Features.Select(x => x.Name)
            });
        }
    }
}
