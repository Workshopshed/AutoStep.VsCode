using AutoStep.Projects;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public interface IProjectHost 
    {
        ProjectConfigurationContext ProjectContext { get; }

        Task Initialize(Uri rootFolder, CancellationToken cancelToken);

        Uri GetPathUri(string relativePath);

        ValueTask WaitForUpToDateBuild(CancellationToken token);

        bool TryGetOpenFile(Uri uri, out ProjectFile file);
        void OpenFile(Uri uri, string documentContent);
        void UpdateOpenFile(Uri uri, string newContent);
        void CloseFile(Uri uri);
        void FileChangedOnDisk(Uri uri);
        void FileCreatedOnDisk(Uri uri);
        void FileDeletedOnDisk(Uri uri);

        void RequestBuild();
    }
}
