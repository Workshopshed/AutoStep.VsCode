using System;
using AutoStep.Extensions;
using AutoStep.Extensions.Abstractions;
using AutoStep.Projects;
using AutoStep.Projects.Files;
using Microsoft.Extensions.Configuration;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Represents a loaded and configured project.
    /// </summary>
    public sealed class ProjectConfigurationContext : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectConfigurationContext"/> class.
        /// </summary>
        /// <param name="project">The autostep project.</param>
        /// <param name="loadedConfiguration">The loaded configuration.</param>
        /// <param name="testFileSet">The set of test files.</param>
        /// <param name="interactionFileSet">The set of interaction files.</param>
        /// <param name="extensions">The loaded extensions.</param>
        public ProjectConfigurationContext(Project project, IConfiguration loadedConfiguration, IFileSet testFileSet, IFileSet interactionFileSet, ILoadedExtensions<IExtensionEntryPoint> extensions)
        {
            Project = project;
            LoadedConfiguration = loadedConfiguration;
            TestFileSet = testFileSet;
            InteractionFileSet = interactionFileSet;
            Extensions = extensions;
        }

        /// <summary>
        /// Gets the AutoStep project.
        /// </summary>
        public Project Project { get; private set; }

        /// <summary>
        /// Gets the loaded configuration.
        /// </summary>
        public IConfiguration LoadedConfiguration { get; private set; }

        /// <summary>
        /// Gets the scanned set of test files.
        /// </summary>
        public IFileSet TestFileSet { get; private set; }

        /// <summary>
        /// Gets the scanned set of interaction files.
        /// </summary>
        public IFileSet InteractionFileSet { get; private set; }

        /// <summary>
        /// Gets the set of loaded extensions.
        /// </summary>
        public ILoadedExtensions<IExtensionEntryPoint> Extensions { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Project = null!;
            TestFileSet = null!;
            InteractionFileSet = null!;

            Extensions.Dispose();

            Extensions = null!;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
