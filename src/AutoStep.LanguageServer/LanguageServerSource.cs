using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Language;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Custom AutoStep source that can either use the file system, or an open file.
    /// </summary>
    internal class LanguageServerSource : IContentSource
    {
        private readonly string loadPath;
        private readonly Func<string, OpenFileState?> openStateFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageServerSource"/> class.
        /// </summary>
        /// <param name="relativeName">The relative path to the file (used for naming).</param>
        /// <param name="loadPath">The absolute path to the file on disk.</param>
        /// <param name="openStateFunc">A function that, given the relative name, will return an indicator of open state.</param>
        public LanguageServerSource(string relativeName, string loadPath, Func<string, OpenFileState?> openStateFunc)
        {
            SourceName = relativeName;
            this.loadPath = loadPath;
            this.openStateFunc = openStateFunc;
        }

        /// <inheritdoc/>
        public string SourceName { get; }

        /// <inheritdoc/>
        public async ValueTask<string> GetContentAsync(CancellationToken cancelToken = default)
        {
            var openContent = openStateFunc(SourceName);

            if (openContent is object)
            {
                return openContent.Content;
            }

            return await File.ReadAllTextAsync(loadPath);
        }

        /// <inheritdoc/>
        public DateTime GetLastContentModifyTime()
        {
            var openContent = openStateFunc(SourceName);

            if (openContent is object)
            {
                return openContent.LastModifyTime;
            }

            try
            {
                return File.GetLastWriteTimeUtc(loadPath);
            }
            catch (IOException)
            {
                return DateTime.MinValue;
            }
        }
    }
}
