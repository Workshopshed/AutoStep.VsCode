using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoStep.LanguageServer
{
    public static class ConfigurationExtensions
    {
        public static string[] GetTestFileGlobs(this IConfiguration config)
        {
            return config.GetValue("tests", new[] { "**/*.as" });
        }

        public static string[] GetInteractionFileGlobs(this IConfiguration config)
        {
            return config.GetValue("interactions", new[] { "**/*.asi" });
        }
    }
}
