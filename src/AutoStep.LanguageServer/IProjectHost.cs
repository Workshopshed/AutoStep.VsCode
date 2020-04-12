using AutoStep.Projects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
    public interface IProjectHost 
    {
        ProjectConfigurationContext ProjectContext { get; }

        void Initialize(Uri rootFolder);

        Uri GetPathUri(string relativePath);

        IEnumerable<T> GetProjectFilesOfType<T>() where T : ProjectFile;

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
