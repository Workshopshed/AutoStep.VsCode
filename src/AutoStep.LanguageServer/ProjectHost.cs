using AutoStep.Extensions;
using AutoStep.Language;
using AutoStep.LanguageServer.Tasks;
using AutoStep.Projects;
using AutoStep.Projects.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using NuGet.Packaging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    public class ProjectConfigurationContext : IDisposable
    {
        public Project Project { get; set; }

        public IConfiguration LoadedConfiguration { get; set; }

        public IFileSet TestFileSet { get; set; }

        public IFileSet InteractionFileSet { get; set; }

        public IExtensionSet Extensions { get; set; }

        public void Dispose()
        {
            Project = null;
            TestFileSet = null;
            InteractionFileSet = null;
            
            Extensions.Dispose();

            Extensions = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    internal class OpenFileState
    {
        public string Content { get; set; }

        public DateTime LastModifyTime { get; set; }
    }

    internal class ProjectHost : IProjectHost
    {
        private readonly ILanguageServer server;
        private readonly ILanguageTaskQueue taskQueue;
        private readonly ILoggerFactory logFactory;
        private readonly ILogger<ProjectHost> logger;

        private readonly ConcurrentDictionary<string, OpenFileState> openContent = new ConcurrentDictionary<string, OpenFileState>();

        private int currentBackgroundTasks;
        private ConcurrentQueue<Action> buildCompletion = new ConcurrentQueue<Action>();
        private ConcurrentQueue<Action> projectReady = new ConcurrentQueue<Action>();

        public ProjectHost(ILanguageServer server, ILanguageTaskQueue taskQueue, ILoggerFactory logFactory, ILogger<ProjectHost> logger)
        {
            this.server = server;
            this.taskQueue = taskQueue;
            this.logFactory = logFactory;
            this.logger = logger;
        }

        public ProjectConfigurationContext ProjectContext { get; private set; }

        public bool IsProjectReady()
        {
            return ProjectContext != null;
        }

        public Uri RootFolder { get; private set; }

        public DirectoryInfo RootDirectoryInfo { get; private set; }

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

            RootDirectoryInfo = new DirectoryInfo(RootFolder.LocalPath.TrimStart('/'));

            InitiateBackgroundProjectLoad();
        }

        private void InitiateBackgroundProjectLoad()
        {
            // Need to unload the current project first.
            RunInBackground(this, async (host, cancelToken) =>
            {
                await LoadConfiguredProject(cancelToken);

                // If the current project is present, load nothing.
                InitiateBackgroundBuild();
            });
        }

        private void ClearProject()
        {
            ProjectContext.Dispose();            
            ProjectContext = null;
        }

        private async Task LoadConfiguredProject(CancellationToken cancelToken)
        {
            try
            {
                // Load the configuration file.
                var config = GetConfiguration(RootDirectoryInfo.FullName);

                // Define file sets for interaction and test.
                var interactionFiles = FileSet.Create(RootDirectoryInfo.FullName, config.GetInteractionFileGlobs(), new string[] { ".autostep/**" });
                var testFiles = FileSet.Create(RootDirectoryInfo.FullName, config.GetTestFileGlobs(), new string[] { ".autostep/**" });

                if (ProjectContext is object)
                {
                    ClearProject();
                }

                var newProject = new Project(true);

                IExtensionSet extensions = null;

                try
                {
                    extensions = await LoadExtensionsAsync(logFactory, config, cancelToken);

                    // Let our extensions extend the project.
                    extensions.AttachToProject(config, newProject);

                    // Add any files from extension content.
                    // Treat the extension directory as two file sets (one for interactions, one for test).
                    var extInteractionFiles = FileSet.Create(extensions.ExtensionsRootDir, new string[] { "*/content/**/*.asi" });
                    var extTestFiles = FileSet.Create(extensions.ExtensionsRootDir, new string[] { "*/content/**/*.as" });

                    newProject.MergeInteractionFileSet(extInteractionFiles);
                    newProject.MergeTestFileSet(extTestFiles);
                }
                catch
                {
                    // Dispose of the extensions if they are set - want to make sure we unload trouble-some extensions.
                    if(extensions is object)
                    {
                        extensions.Dispose();
                    }

                    throw;
                }

                // Add the two file sets.
                newProject.MergeInteractionFileSet(interactionFiles, GetProjectFileSource);
                newProject.MergeTestFileSet(testFiles, GetProjectFileSource);

                ProjectContext = new ProjectConfigurationContext
                {
                    LoadedConfiguration = config,
                    Project = newProject,
                    TestFileSet = testFiles,
                    InteractionFileSet = interactionFiles,
                    Extensions = extensions
                };
            }
            catch (ProjectConfigurationException ex)
            {
                // Feed diagnostics back.
                // An error occurred.
                // We won't have a project context any more.
                server.Window.ShowError($"There is a problem with the project configuration: {ex.Message}");
            }
            catch (Exception ex)
            {
                server.Window.ShowError($"Failed to load project: {ex.Message}");
            }
        }

        private IContentSource GetProjectFileSource(FileSetEntry fileEntry)
        {
            return new LanguageServerSource(fileEntry.Relative, fileEntry.Absolute, (relative) => 
            {
                if (openContent.TryGetValue(relative, out var result))
                {
                    return result;
                }

                return null;
            });
        }

        protected async Task<IExtensionSet> LoadExtensionsAsync(ILoggerFactory logFactory, IConfiguration projectConfig, CancellationToken cancelToken)
        {
            var sourceSettings = new ExtensionSourceSettings(RootDirectoryInfo.FullName);

            var customSources = projectConfig.GetSection("extensionSources").Get<string[]>() ?? Array.Empty<string>();

            if (customSources.Length > 0)
            {
                // Add any additional configured sources.
                sourceSettings.AppendCustomSources(customSources);
            }

            var loaded = await ExtensionSetLoader.LoadExtensionsAsync(RootDirectoryInfo.FullName, Assembly.GetEntryAssembly(), sourceSettings, logFactory, projectConfig, cancelToken);

            return loaded;
        }

        protected IConfiguration GetConfiguration(string rootDirectory, string explicitConfigFile = null)
        {
            var configurationBuilder = new ConfigurationBuilder();

            FileInfo configFile;

            if (explicitConfigFile is null)
            {
                configFile = new FileInfo(Path.Combine(rootDirectory, "autostep.config.json"));
            }
            else
            {
                configFile = new FileInfo(explicitConfigFile);
            }

            // Is there a config file?
            if (configFile.Exists)
            {
                // Add the JSON file.
                configurationBuilder.AddJsonFile(configFile.FullName);
            }

            // Add environment.
            configurationBuilder.AddEnvironmentVariables("AutoStep");

            // TODO: We might allow config options to come from client settings, but not yet.
            // configurationBuilder.AddInMemoryCollection(args.Option);

            return configurationBuilder.Build();
        }

        public Uri GetPathUri(string relativePath)
        {
            // We're going to have to actually look up the file.
            if (ProjectContext is object && ProjectContext.Project.AllFiles.TryGetValue(relativePath, out var file))
            {
                if(file is IProjectFileFromSet fromSet)
                {
                    return new Uri(Path.GetFullPath(relativePath, fromSet.RootPath));
                }
            }

            return null;
        }

        private string RelativePathFromUri(Uri fileUri)
        {
            return Path.GetRelativePath(RootFolder.LocalPath, fileUri.LocalPath);
        }

        public bool TryGetOpenFile(Uri uri, out ProjectFile file)
        {
            var path = RelativePathFromUri(uri);

            if(openContent.ContainsKey(path) && ProjectContext is object && ProjectContext.Project.AllFiles.TryGetValue(path, out file))
            {
                return true;
            }

            file = null;
            return false;
        }

        public void OpenFile(Uri uri, string documentContent)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            openContent[name] = new OpenFileState
            {
                Content = documentContent,
                LastModifyTime = DateTime.UtcNow
            };

            InitiateBackgroundBuild();
        }

        public void UpdateOpenFile(Uri uri, string newContent)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            // Look at the set of files in the project.
            if (openContent.TryGetValue(name, out var state))
            {
                state.Content = newContent;
                state.LastModifyTime = DateTime.UtcNow;

                InitiateBackgroundBuild();
            }
            else
            {
                // ? File doesn't exist...
                logger.LogError("Cannot update file, not open: {0}", uri);
            }
        }


        public void CloseFile(Uri uri)
        {
            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            // Just remove from the set of open content.
            openContent.Remove(name, out var _);
        }

        private void IssueDiagnosticsForFile(string path, ProjectFile file)
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

            var vsCodeUri = new Uri(RootFolder, path);

            var diagnosticParams = new PublishDiagnosticsParams
            {
                Uri = vsCodeUri,
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

        public ValueTask WaitForUpToDateBuild(CancellationToken token)
        {
            if (currentBackgroundTasks == 0)
            {
                return default;
            }

            var completionSource = new TaskCompletionSource<object>();
            token.Register(() =>
            {
                if (!completionSource.Task.IsCompleted)
                {
                    completionSource.SetCanceled();
                }
            });

            buildCompletion.Enqueue(() =>
            {
                completionSource.SetResult(null);
            });

            return new ValueTask(completionSource.Task);
        }

        private void InitiateBackgroundBuild()
        {
            // Queue a compilation task.
            RunInBackground(this, async (projHost, cancelToken) =>
            {
                // Only run a build if this is the only background task.
                // All the other background tasks queue a build, but we only want to build
                // when everything else is done.
                // We also don't want to try and build if there is no project context available.
                if(currentBackgroundTasks > 1 || ProjectContext is null)
                {
                    return;
                }

                var project = projHost.ProjectContext.Project;

                var builder = project.Compiler;

                await builder.CompileAsync(logFactory, cancelToken);

                if (!cancelToken.IsCancellationRequested)
                {
                    builder.Link(cancelToken);
                }

                if (!cancelToken.IsCancellationRequested)
                {
                    // Notify done.
                    projHost.OnProjectCompiled(project);
                }
            });
        }

        public void OnProjectCompiled(Project project)
        {
            // On project compilation, go through the open files and feed diagnostics back.
            // Go through our open files, feed diagnostics back.
            foreach (var openPath in openContent.Keys)
            {
                if(project.AllFiles.TryGetValue(openPath, out var file))
                {
                    IssueDiagnosticsForFile(openPath, file);
                }
            }

            // Inform the client that a build has just finished.
            server.SendNotification("autostep/build_complete");
        }

        public void FileCreatedOnDisk(Uri uri)
        {
            RunInBackground(uri, (uri, cancelToken) =>
            {
                if (ProjectContext is object)
                {
                    var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);
                    var extension = Path.GetExtension(name);

                    // Add the project file to the set.
                    if (extension == ".as")
                    {
                        if (ProjectContext.TestFileSet.TryAddFile(name))
                        {
                            ProjectContext.Project.MergeTestFileSet(ProjectContext.TestFileSet, GetProjectFileSource);

                            InitiateBackgroundBuild();
                        }
                    }
                    else if (extension == ".asi")
                    {
                        if (ProjectContext.InteractionFileSet.TryAddFile(name))
                        {
                            ProjectContext.Project.MergeInteractionFileSet(ProjectContext.InteractionFileSet, GetProjectFileSource);

                            InitiateBackgroundBuild();
                        }
                    }
                }

                return default;
            });
        }

        public void FileChangedOnDisk(Uri uri)
        {
            if (Path.GetFileName(uri.LocalPath).Equals("autostep.config.json", StringComparison.CurrentCultureIgnoreCase))
            {
                // Config has changed, reload the project.
                InitiateBackgroundProjectLoad();
            }
        }

        public void FileDeletedOnDisk(Uri uri)
        {
            if (ProjectContext is object)
            {
                RunInBackground(uri, (uri, cancelToken) =>
                {
                    var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);
                    var extension = Path.GetExtension(name);

                    // Add the project file to the set.
                    if (extension == ".as")
                    {
                        if (ProjectContext.TestFileSet.TryRemoveFile(name))
                        {
                            ProjectContext.Project.MergeTestFileSet(ProjectContext.TestFileSet);

                            InitiateBackgroundBuild();
                        }
                    }
                    else if (extension == ".asi")
                    {
                        if (ProjectContext.InteractionFileSet.TryRemoveFile(name))
                        {
                            ProjectContext.Project.MergeInteractionFileSet(ProjectContext.InteractionFileSet);

                            InitiateBackgroundBuild();
                        }
                    }

                    return default;
                });
            }
        }

        public void RequestBuild()
        {
            InitiateBackgroundBuild();
        }

        private void RunInBackground<TArgs>(TArgs arg, Func<TArgs, CancellationToken, ValueTask> callback)
        {
            Interlocked.Increment(ref currentBackgroundTasks);

            taskQueue.QueueTask(arg, async (arg, cancelToken) => 
            {
                await callback(arg, cancelToken);

                if (Interlocked.Decrement(ref currentBackgroundTasks) == 0)
                {
                    // Dequeue all the things.
                    while (buildCompletion.TryDequeue(out var invoke))
                    {
                        invoke();
                    }
                }
            });
        }
    }
}
