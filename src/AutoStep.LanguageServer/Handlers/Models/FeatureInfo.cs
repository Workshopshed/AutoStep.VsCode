namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Represents a block of feature info sent back to the extension client.
    /// </summary>
    public class FeatureInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureInfo"/> class.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="name">The name of the feature.</param>
        /// <param name="description">The description body.</param>
        public FeatureInfo(string sourceFile, string name, string? description)
        {
            SourceFile = sourceFile;
            Name = name;
            Description = description;
        }

        /// <summary>
        /// Gets the source file relative path for the feature.
        /// </summary>
        public string SourceFile { get; }

        /// <summary>
        /// Gets the name of the feature.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description content for the feature.
        /// </summary>
        public string? Description { get; }
    }
}
