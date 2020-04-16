using AutoStep.Extensions;
using Microsoft.Extensions.Configuration;
using System.Linq;

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

        /// <summary>
        /// Get the set of configured extensions.
        /// </summary>
        /// <param name="config">The configuration set.</param>
        /// <returns>The set of extensions.</returns>
        public static ExtensionConfiguration[] GetExtensionConfiguration(this IConfiguration config)
        {
            var all = config.GetSection("extensions").Get<ExtensionConfiguration[]>();

            if (all.Any(p => string.IsNullOrWhiteSpace(p.Package)))
            {
                throw new ProjectConfigurationException("Extensions must have a specified Package Id.");
            }

            return all;
        }
    }
}
