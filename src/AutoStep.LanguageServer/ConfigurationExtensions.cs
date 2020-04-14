using Microsoft.Extensions.Configuration;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Extensions to the configuration file that help access common configuration properties.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Gets the set of test file globs.
        /// </summary>
        /// <param name="config">The project config.</param>
        /// <returns>A set of glob paths.</returns>
        public static string[] GetTestFileGlobs(this IConfiguration config)
        {
            return config.GetValue("tests", new[] { "**/*.as" });
        }

        /// <summary>
        /// Gets the set of interaction file globs.
        /// </summary>
        /// <param name="config">The project config.</param>
        /// <returns>A set of glob paths.</returns>
        public static string[] GetInteractionFileGlobs(this IConfiguration config)
        {
            return config.GetValue("interactions", new[] { "**/*.asi" });
        }
    }
}
