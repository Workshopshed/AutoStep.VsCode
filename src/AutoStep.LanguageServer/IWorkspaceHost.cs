using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions.Interaction;
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Test;
using AutoStep.Language.Test.Matching;
using AutoStep.Projects;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Defines the service for the central workspace host.
    /// </summary>
    public interface IWorkspaceHost
    {
        /// <summary>
        /// Initialise the workspace in a given folder.
        /// </summary>
        /// <param name="rootFolder">The root folder.</param>
        void Initialise(Uri rootFolder);

        /// <summary>
        /// Gets a vscode-compatible absolute URI from a relative path.
        /// </summary>
        /// <param name="relativePath">The relative path.</param>
        /// <returns>The URI (if it can be determined).</returns>
        Uri? GetPathUri(string relativePath);

        /// <summary>
        /// Gets all available project files of the given type.
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <returns>The set of files.</returns>
        IEnumerable<T> GetProjectFilesOfType<T>()
            where T : ProjectFile;

        /// <summary>
        /// Provides a task that completes once up-tp-date build information is available (after any document changes, etc).
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <returns>An awaitable task.</returns>
        ValueTask WaitForUpToDateBuild(CancellationToken token);

        /// <summary>
        /// Attempts to retrieve an open file in the workspace.
        /// </summary>
        /// <param name="uri">The provided URI.</param>
        /// <param name="file">The project file.</param>
        /// <returns>True if the file is open and available.</returns>
        bool TryGetOpenFile(Uri uri, [NotNullWhen(true)] out ProjectFile? file);

        /// <summary>
        /// Indicates that the workspace host should open a file in the workspace.
        /// </summary>
        /// <param name="uri">The URI of the file.</param>
        /// <param name="documentContent">The in-memory content of the file.</param>
        void OpenFile(Uri uri, string documentContent);

        /// <summary>
        /// Indicates that the workspace host should update the in-memory content of a file in the workspace.
        /// </summary>
        /// <param name="uri">The URI of the file.</param>
        /// <param name="newContent">The new content of the file.</param>
        void UpdateOpenFile(Uri uri, string newContent);

        /// <summary>
        /// Indicates that the workspace host should 'close' a file, and revert to the on-disk state.
        /// </summary>
        /// <param name="uri">The file URI.</param>
        void CloseFile(Uri uri);

        /// <summary>
        /// Indicates that a watched file has changed on disk.
        /// </summary>
        /// <param name="uri">The file that has changed.</param>
        void FileChangedOnDisk(Uri uri);

        /// <summary>
        /// Indicates that a watched file has been created on disk.
        /// </summary>
        /// <param name="uri">The file that has been created.</param>
        void FileCreatedOnDisk(Uri uri);

        /// <summary>
        /// Indicates that a warch file has been deleted on disk.
        /// </summary>
        /// <param name="uri">The file that has been deleted.</param>
        void FileDeletedOnDisk(Uri uri);

        /// <summary>
        /// Get the possible step definition matches for a given step reference.
        /// </summary>
        /// <param name="stepRef">The step reference.</param>
        /// <returns>The set of matches.</returns>
        IEnumerable<IMatchResult> GetPossibleStepDefinitions(StepReferenceElement stepRef);

        InteractionMethod? GetMethodDefinition(MethodCallElement methodCall, InteractionDefinitionElement containingElement);
    }
}
