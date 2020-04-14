using System.Collections.Generic;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Defines the result of a feature request.
    /// </summary>
    public class FeatureSetResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureSetResult"/> class.
        /// </summary>
        /// <param name="features">The feature set.</param>
        public FeatureSetResult(IEnumerable<FeatureInfo> features)
        {
            Features = features;
        }

        /// <summary>
        /// Gets the set of features.
        /// </summary>
        public IEnumerable<FeatureInfo> Features { get; }
    }
}
