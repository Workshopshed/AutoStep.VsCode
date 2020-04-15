﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions.Interaction;
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Test;
using AutoStep.Extensions;
using AutoStep.Language;
using AutoStep.Language.Interaction;
using AutoStep.Language.Test.Matching;
using AutoStep.LanguageServer.Tasks;
using AutoStep.Projects;
using AutoStep.Projects.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Centralised management of the workspace state.
    /// </summary>
    public class WorkspaceHost : IWorkspaceHost
    {
        private readonly ILanguageServer server;
        private readonly ILanguageTaskQueue taskQueue;
        private readonly ILoggerFactory logFactory;
        private readonly ILogger<WorkspaceHost> logger;
        private readonly ConcurrentQueue<Action> buildCompletion = new ConcurrentQueue<Action>();
        private readonly ConcurrentDictionary<string, OpenFileState> openContent = new ConcurrentDictionary<string, OpenFileState>();

        private int currentBackgroundTasks;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkspaceHost"/> class.
        /// </summary>
        /// <param name="server">The language server.</param>
        /// <param name="taskQueue">The language task queue.</param>
        /// <param name="logFactory">The log factory.</param>
        /// <param name="logger">The workspace host logger.</param>
        public WorkspaceHost(ILanguageServer server, ILanguageTaskQueue taskQueue, ILoggerFactory logFactory, ILogger<WorkspaceHost> logger)
        {
            this.server = server;
            this.taskQueue = taskQueue;
            this.logFactory = logFactory;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the current loaded project context.
        /// </summary>
        public ProjectConfigurationContext? ProjectContext { get; private set; }

        /// <summary>
        /// Gets the workspace root folder.
        /// </summary>
        public Uri? RootFolder { get; private set; }

        /// <summary>
        /// Gets the workspace root directory info.
        /// </summary>
        public DirectoryInfo? RootDirectoryInfo { get; private set; }

        /// <inheritdoc/>
        public void Initialise(Uri rootFolder)
        {
            if (rootFolder is null)
            {
                throw new ArgumentNullException(nameof(rootFolder));
            }

            if (rootFolder.AbsoluteUri.EndsWith("/", StringComparison.InvariantCulture))
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

        /// <inheritdoc/>
        public Uri? GetPathUri(string relativePath)
        {
            // We're going to have to actually look up the file.
            if (ProjectContext is object && ProjectContext.Project.AllFiles.TryGetValue(relativePath, out var file))
            {
                if (file is IProjectFileFromSet fromSet)
                {
                    return new Uri(Path.GetFullPath(relativePath, fromSet.RootPath));
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public bool TryGetOpenFile(Uri uri, [NotNullWhen(true)] out ProjectFile? file)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var path = RelativePathFromUri(uri);

            if (openContent.ContainsKey(path) && ProjectContext is object && ProjectContext.Project.AllFiles.TryGetValue(path, out file))
            {
                return true;
            }

            file = null;
            return false;
        }

        /// <inheritdoc/>
        public void OpenFile(Uri uri, string documentContent)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (RootFolder is null)
            {
                // Something has called this method before the Initialise method has been called...
                throw new InvalidOperationException();
            }

            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            openContent[name] = new OpenFileState
            {
                Content = documentContent,
                LastModifyTime = DateTime.UtcNow,
            };

            InitiateBackgroundBuild();
        }

        /// <inheritdoc/>
        public void UpdateOpenFile(Uri uri, string newContent)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (RootFolder is null)
            {
                // Something has called this method before the Initialise method has been called...
                throw new InvalidOperationException();
            }

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
                logger.LogError(WorkspaceHostMessages.FileToUpdateNotOpen, uri);
            }
        }

        /// <inheritdoc/>
        public void CloseFile(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (RootFolder is null)
            {
                // Something has called this method before the Initialise method has been called...
                throw new InvalidOperationException();
            }

            var name = Path.GetRelativePath(RootFolder.LocalPath, uri.LocalPath);

            // Just remove from the set of open content.
            openContent.Remove(name, out var _);
        }

        /// <inheritdoc/>
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
                completionSource.SetResult(null!);
            });

            return new ValueTask(completionSource.Task);
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetProjectFilesOfType<T>()
            where T : ProjectFile
        {
            if (ProjectContext is null)
            {
                return Enumerable.Empty<T>();
            }

            return ProjectContext.Project.AllFiles.Values.OfType<T>();
        }

        /// <inheritdoc/>
        public void FileCreatedOnDisk(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (IsUriConfigFile(uri))
            {
                // Config has changed, reload the project.
                InitiateBackgroundProjectLoad();
            }
            else
            {
                RunInBackground(uri, (uri, cancelToken) =>
                {
                    if (ProjectContext is object)
                    {
                        var name = Path.GetRelativePath(RootFolder!.LocalPath, uri.LocalPath);
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
        }

        /// <inheritdoc/>
        public void FileChangedOnDisk(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (IsUriConfigFile(uri))
            {
                // Config has changed, reload the project.
                InitiateBackgroundProjectLoad();
            }
        }

        /// <inheritdoc/>
        public void FileDeletedOnDisk(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (IsUriConfigFile(uri))
            {
                // Config has changed, reload the project.
                InitiateBackgroundProjectLoad();
            }
            else
            {
                RunInBackground(uri, (uri, cancelToken) =>
                {
                    var name = Path.GetRelativePath(RootFolder!.LocalPath, uri.LocalPath);
                    var extension = Path.GetExtension(name);

                    if (ProjectContext is null)
                    {
                        // Can't do anything if the project isn't loaded; file scanning will pick up the change
                        // momentarily.
                        return default;
                    }

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

        /// <inheritdoc/>
        public IEnumerable<IMatchResult> GetPossibleStepDefinitions(StepReferenceElement stepRef)
        {
            if (ProjectContext is null)
            {
                return Enumerable.Empty<IMatchResult>();
            }

            return ProjectContext.Project.Compiler.GetPossibleStepDefinitions(stepRef);
        }

        /// <inheritdoc/>
        public InteractionMethod? GetMethodDefinition(MethodCallElement methodCall, InteractionDefinitionElement containingElement)
        {
            if (methodCall is null)
            {
                throw new ArgumentNullException(nameof(methodCall));
            }

            if (containingElement is null)
            {
                throw new ArgumentNullException(nameof(containingElement));
            }

            var methodTable = GetMethodTableForInteractionDefinition(containingElement);

            if (methodTable is object && methodTable.TryGetMethod(methodCall.MethodName, out var definition))
            {
                return definition;
            }

            return null;
        }

        /// <inheritdoc/>
        public MethodTable? GetMethodTableForInteractionDefinition(InteractionDefinitionElement containingElement)
        {
            if (containingElement is null)
            {
                throw new ArgumentNullException(nameof(containingElement));
            }

            if (ProjectContext is object)
            {
                // Get the method table.
                var interactionSet = ProjectContext.Project.Compiler.GetCurrentInteractionSet();

                if (interactionSet?.ExtendedMethodReferences is object)
                {
                    return interactionSet.ExtendedMethodReferences.GetMethodTableForElement(containingElement);
                }
            }

            return null;
        }

        private void InitiateBackgroundProjectLoad()
        {
            // Load the project in the background (in the task queue).
            RunInBackground(this, async (host, cancelToken) =>
            {
                await LoadConfiguredProject(cancelToken);

                // If the current project is present, load nothing.
                InitiateBackgroundBuild();
            });
        }

        private void InitiateBackgroundBuild()
        {
            // Queue a compilation task.
            RunInBackground(this, async (workspace, cancelToken) =>
            {
                // Only run a build if this is the only background task.
                // All the other background tasks queue a build, but we only want to build
                // when everything else is done.
                // We also don't want to try and build if there is no project context available.
                if (ProjectContext is null || currentBackgroundTasks > 1)
                {
                    return;
                }

                var project = workspace.ProjectContext!.Project;

                var builder = project.Compiler;

                await builder.CompileAsync(logFactory, cancelToken);

                if (!cancelToken.IsCancellationRequested)
                {
                    builder.Link(cancelToken);
                }

                if (!cancelToken.IsCancellationRequested)
                {
                    // Notify done.
                    workspace.OnProjectCompiled(project);
                }
            });
        }

        private void OnProjectCompiled(Project project)
        {
            // On project compilation, go through the open files and feed diagnostics back.
            // Go through our open files, feed diagnostics back.
            foreach (var openPath in openContent.Keys)
            {
                if (project.AllFiles.TryGetValue(openPath, out var file))
                {
                    IssueDiagnosticsForFile(openPath, file);
                }
            }

            // Inform the client that a build has just finished.
            server.SendNotification("autostep/build_complete");
        }

        private bool IsUriConfigFile(Uri uri)
        {
            return Path.GetFileName(uri.LocalPath).Equals("autostep.config.json", StringComparison.CurrentCultureIgnoreCase);
        }

        private void ClearProject()
        {
            if (ProjectContext is object)
            {
                ProjectContext.Dispose();
                ProjectContext = null;
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Want to report all project load errors oursleves.")]
        private async Task LoadConfiguredProject(CancellationToken cancelToken)
        {
            try
            {
                if (RootDirectoryInfo is null)
                {
                    // Not sure how we would get here; InitiateBackgroundProjectLoad would have
                    // to have been called before Initialise, which definitely shouldn't happen.
                    throw new InvalidOperationException();
                }

                // Load the configuration file.
                var config = GetConfiguration(RootDirectoryInfo.FullName);

                // Define file sets for interaction and test.
                var interactionFiles = FileSet.Create(RootDirectoryInfo.FullName, config.GetInteractionFileGlobs(), new string[] { ".autostep/**" });
                var testFiles = FileSet.Create(RootDirectoryInfo.FullName, config.GetTestFileGlobs(), new string[] { ".autostep/**" });

                if (ProjectContext is object)
                {
                    // This needs to happen in another method to ensure that extensions can unload correctly.
                    // GC can leave type references behind if still in the same method scope.
                    ClearProject();
                }

                var newProject = new Project(true);

                IExtensionSet? extensions = null;

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
                    if (extensions is object)
                    {
                        extensions.Dispose();
                    }

                    throw;
                }

                // Add the two file sets.
                newProject.MergeInteractionFileSet(interactionFiles, GetProjectFileSource);
                newProject.MergeTestFileSet(testFiles, GetProjectFileSource);

                ProjectContext = new ProjectConfigurationContext(
                    newProject,
                    config,
                    testFiles,
                    interactionFiles,
                    extensions);
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

        private async Task<IExtensionSet> LoadExtensionsAsync(ILoggerFactory logFactory, IConfiguration projectConfig, CancellationToken cancelToken)
        {
            var sourceSettings = new ExtensionSourceSettings(RootDirectoryInfo!.FullName);

            var customSources = projectConfig.GetSection("extensionSources").Get<string[]>() ?? Array.Empty<string>();

            if (customSources.Length > 0)
            {
                // Add any additional configured sources.
                sourceSettings.AppendCustomSources(customSources);
            }

            var loaded = await ExtensionSetLoader.LoadExtensionsAsync(RootDirectoryInfo.FullName, sourceSettings, logFactory, projectConfig, cancelToken);

            return loaded;
        }

        private static IConfiguration GetConfiguration(string rootDirectory, string? explicitConfigFile = null)
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

        private string RelativePathFromUri(Uri fileUri)
        {
            if (RootFolder is null)
            {
                // Something has called this method before the Initialise method has been called...
                throw new InvalidOperationException();
            }

            return Path.GetRelativePath(RootFolder.LocalPath, fileUri.LocalPath);
        }

        private void IssueDiagnosticsForFile(string path, ProjectFile file)
        {
            LanguageOperationResult? primary = null;
            LanguageOperationResult? secondary = null;

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

            var vsCodeUri = new Uri(RootFolder!, path);

            var diagnosticParams = new PublishDiagnosticsParams
            {
                Uri = vsCodeUri,
                Diagnostics = new Container<Diagnostic>(messages.Select(DiagnosticFromMessage)),
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
                Range = new Range(new Position(msg.StartLineNo - 1, msg.StartColumn - 1), new Position((msg.EndLineNo ?? msg.StartLineNo) - 1, endPosition.Value - 1)),
            };
        }
    }
}
