using MediatR;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Request for feature data.
    /// </summary>
    public class FeatureRequest : IRequest<FeatureSetResult>
    {
    }
}
