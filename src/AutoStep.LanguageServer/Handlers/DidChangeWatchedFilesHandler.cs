using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// File handler for watched files changing.
    /// </summary>
    public class DidChangeWatchedFilesHandler : IDidChangeWatchedFilesHandler
    {
        private readonly IWorkspaceHost workspaceHost;
        private DidChangeWatchedFilesCapability capability;

        /// <summary>
        /// Initializes a new instance of the <see cref="DidChangeWatchedFilesHandler"/> class.
        /// </summary>
        /// <param name="workspaceHost">The workspace host.</param>
        public DidChangeWatchedFilesHandler(IWorkspaceHost workspaceHost)
        {
            this.workspaceHost = workspaceHost;
            capability = new DidChangeWatchedFilesCapability();
        }

        /// <inheritdoc/>
        public object GetRegistrationOptions()
        {
            return new object();
        }

        /// <summary>
        /// Handles a file change.
        /// </summary>
        /// <param name="request">The request details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Completion.</returns>
        public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new System.ArgumentNullException(nameof(request));
            }

            foreach (var change in request.Changes)
            {
                switch (change.Type)
                {
                    case FileChangeType.Created:
                    {
                        workspaceHost.FileCreatedOnDisk(change.Uri);
                        break;
                    }

                    case FileChangeType.Changed:
                    {
                        workspaceHost.FileChangedOnDisk(change.Uri);
                        break;
                    }

                    case FileChangeType.Deleted:
                    {
                        workspaceHost.FileDeletedOnDisk(change.Uri);
                        break;
                    }
                }
            }

            return Unit.Task;
        }

        /// <inheritdoc/>
        public void SetCapability(DidChangeWatchedFilesCapability capability)
        {
            this.capability = capability;
        }
    }
}
