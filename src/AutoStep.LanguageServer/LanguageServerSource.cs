using AutoStep.Language;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public class LanguageServerSource : IContentSource
    {
        private readonly string loadPath;
        
        public LanguageServerSource(string relativeName, string loadPath)
        {
            SourceName = relativeName;
            this.loadPath = loadPath;
        }

        public string SourceName { get; }

        public string UnsavedContent { get; private set; }

        public DateTime? LastUnsavedUpdate { get; private set; }

        public void UpdateUnsavedContent(string content)
        {
            UnsavedContent = content;
            LastUnsavedUpdate = DateTime.UtcNow;
        }

        public void ResetFromDisk()
        {
            LastUnsavedUpdate = null;
            UnsavedContent = null;
        }

        public async ValueTask<string> GetContentAsync(CancellationToken cancelToken = default)
        {
            return UnsavedContent ?? await File.ReadAllTextAsync(loadPath);
        }

        public DateTime GetLastContentModifyTime()
        {
            if(LastUnsavedUpdate is DateTime)
            {
                return LastUnsavedUpdate.Value;
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
