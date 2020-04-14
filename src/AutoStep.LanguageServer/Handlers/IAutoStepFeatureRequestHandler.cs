using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// JSON-RPC handler interface for autostep feature data.
    /// </summary>
    [Method("autostep/features")]
    public interface IAutoStepFeatureRequestHandler : IRequestHandler<FeatureRequest, FeatureSetResult>, IJsonRpcRequestHandler<FeatureRequest, FeatureSetResult>
    {
    }
}
