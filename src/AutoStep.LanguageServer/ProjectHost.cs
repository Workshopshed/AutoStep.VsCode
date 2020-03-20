using AutoStep.Language;
using AutoStep.Projects;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    public interface IProjectHost 
    {
        Project Project { get; }

        void Initialize(Uri rootFolder);
        void OnProjectCompiled();

        void OpenFile(Uri uri, string documentContent);
        void UpdateOpenFile(Uri uri, string newContent);
        void CloseFile(Uri uri);
        void FileChangedOnDisk(Uri uri);
        void FileCreatedOnDisk(Uri uri);
        void FileDeletedOnDisk(Uri uri);

        void RequestBuild();
    }

    public class ProjectHost : IProjectHost
    {
        private readonly ILanguageServer server;
        private readonly ICompilationTaskQueue taskQueue;
        private readonly ILogger<ProjectHost> logger;
        private readonly Dictionary<Uri, ProjectFile> openFiles = new Dictionary<Uri, ProjectFile>();
        private readonly object lockObj = new object();

        public ProjectHost(ILanguageServer server, ICompilationTaskQueue taskQueue, ILogger<ProjectHost> logger)
        {
            this.server = server;
            this.taskQueue = taskQueue;
            this.logger = logger;
            this.Project = new Project();
        }

        public Project Project { get; }

        public Uri RootFolder { get; private set; }

        public void Initialize(Uri rootFolder)
        {
            if (rootFolder.AbsoluteUri.EndsWith("/"))
            {
                RootFolder = rootFolder;
            }
            else
            {
                RootFolder = new Uri(rootFolder.AbsoluteUri + "/");
            }

            var dirInfo = new DirectoryInfo(RootFolder.LocalPath.TrimStart('/'));

            foreach(var file in dirInfo.EnumerateFiles("*.*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                var ext = Path.GetExtension(file.FullName);

                if(ext == ".as" || ext == ".asi")
                {
                    // Create a 'vscode' format URI.
                    var relativePath = Path.GetRelativePath(dirInfo.FullName, file.FullName);

                    var vsCodeUri = new Uri(RootFolder, relativePath);

                    AddProjectFile(vsCodeUri);
                }
            }

            InitiateBackgroundBuild();
        }

        private ProjectFile AddProjectFile(Uri uri)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            var source = new LanguageServerSource(name, uri.LocalPath);

            var extension = Path.GetExtension(name);

            if (extension == ".as")
            {
                // Add a test file.
                var testFile = new ProjectTestFile(name, source);

                Project.TryAddFile(testFile);

                return testFile;
            }
            else if (extension == ".asi")
            {
                // Add an interaction file. 
                var intFile = new ProjectInteractionFile(name, source);

                Project.TryAddFile(intFile);

                return intFile;
            }

            return null;
        }

        public void OnProjectCompiled()
        {
            // On project compilation, go through the open files and feed diagnostics back.
            // Go through our open files, feed diagnostics back.
            foreach (var file in openFiles)
            {
                IssueDiagnosticsForFile(file.Key, file.Value);
            }
        }

        public void OpenFile(Uri uri, string documentContent)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            // Look at the set of files in the project.
            if (!Project.AllFiles.TryGetValue(name, out ProjectFile projFile))
            {
                // Create the file.
                projFile = AddProjectFile(uri);
            }

            openFiles.Add(uri, projFile);

            // Change the source.
            var source = (LanguageServerSource)projFile.ContentSource;

            source.UpdateUnsavedContent(documentContent);

            InitiateBackgroundBuild();            
        }

        public void CloseFile(Uri uri)
        {
            // Look at the set of files in the project.
            if (openFiles.TryGetValue(uri, out ProjectFile projFile))
            {
                openFiles.Remove(uri);

                // Change the source.
                var source = (LanguageServerSource)projFile.ContentSource;

                source.ResetFromDisk();
            }
            else
            {
                // ? File doesn't exist...
                logger.LogError("Cannot open file, not in project: {0}", uri);
            }
        }

        public void UpdateOpenFile(Uri uri, string newContent)
        {
            // Look at the set of files in the project.
            if (openFiles.TryGetValue(uri, out ProjectFile projFile))
            {
                // Change the source.
                var source = (LanguageServerSource)projFile.ContentSource;

                source.UpdateUnsavedContent(newContent);

                InitiateBackgroundBuild();
            }
            else
            {
                // ? File doesn't exist...
                logger.LogError("Cannot update file, not open: {0}", uri);
            }
        }


        private void IssueDiagnosticsForFile(Uri uri, ProjectFile file)
        {
            LanguageOperationResult primary = null;
            LanguageOperationResult secondary = null;

            if (file is ProjectInteractionFile interactionFile)
            {
                primary = interactionFile.LastCompileResult;

                secondary = interactionFile.LastSetBuildResult;
            }
            else if (file is ProjectTestFile testFile)
            {
                primary = testFile.LastCompileResult;
                secondary = testFile.LastLinkResult;
            }

            IEnumerable<LanguageOperationMessage> messages;

            if (primary is null)
            {
                messages = Enumerable.Empty<LanguageOperationMessage>();
            }
            else
            {
                messages = primary.Messages;

                if (secondary is object)
                {
                    messages = messages.Concat(secondary.Messages);
                }
            }

            var diagnosticParams = new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = new Container<Diagnostic>(messages.Select(DiagnosticFromMessage))
            };

            server.Document.PublishDiagnostics(diagnosticParams);
        }

        private static Diagnostic DiagnosticFromMessage(LanguageOperationMessage msg)
        {
            var severity = msg.Level switch
            {
                CompilerMessageLevel.Error => DiagnosticSeverity.Error,
                CompilerMessageLevel.Warning => DiagnosticSeverity.Warning,
                _ => DiagnosticSeverity.Information
            };

            var endPosition = msg.EndColumn;

            if (endPosition is null)
            {
                endPosition = msg.StartColumn;
            }
            else
            {
                endPosition++;
            }

            // Expand message end to the location after the token
            return new Diagnostic
            {
                Code = new DiagnosticCode($"ASC{(int)msg.Code:D5}"),
                Severity = severity,
                Message = msg.Message,
                Source = "autostep-compiler",
                Range = new Range(new Position(msg.StartLineNo - 1, msg.StartColumn - 1), new Position((msg.EndLineNo ?? msg.StartLineNo) - 1, endPosition.Value - 1))
            };
        }

        private void InitiateBackgroundBuild()
        {
            // Queue a compilation task.
            taskQueue.QueueCompileTask(new CompileTask(this));
        }

        public void FileCreatedOnDisk(Uri uri)
        {
            AddProjectFile(uri);
        }

        public void FileChangedOnDisk(Uri uri)
        {
            // Nothing to do here?
        }

        public void FileDeletedOnDisk(Uri uri)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            // Look at the set of files in the project.
            if (Project.AllFiles.TryGetValue(name, out ProjectFile projFile))
            {
                // Remove the file.
                Project.TryRemoveFile(projFile);
            }
        }

        public void RequestBuild()
        {
            InitiateBackgroundBuild();
        }

    }
}
