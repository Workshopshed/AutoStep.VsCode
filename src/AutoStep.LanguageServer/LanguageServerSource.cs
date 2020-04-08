using AutoStep.Language;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    internal class LanguageServerSource : IContentSource
    {
        private readonly string loadPath;
        private readonly Func<string, OpenFileState> openStateFunc;

        public LanguageServerSource(string relativeName, string loadPath, Func<string, OpenFileState> openStateFunc)
        {
            SourceName = relativeName;
            this.loadPath = loadPath;
            this.openStateFunc = openStateFunc;
        }

        public string SourceName { get; }

        public async ValueTask<string> GetContentAsync(CancellationToken cancelToken = default)
        {
            var openContent = openStateFunc(SourceName);

            if(openContent is object)
            {
                return openContent.Content;
            }
            
            return await File.ReadAllTextAsync(loadPath);
        }

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
            catch(IOException)
            {
                return DateTime.MinValue;
            }
        }
    }
}
