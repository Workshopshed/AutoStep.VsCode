using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Handles updates to open files in the VS Code window.
    /// </summary>
    public sealed class DocumentSyncHandler : ITextDocumentSyncHandler
    {
        private readonly IWorkspaceHost projectHost;

        private readonly DocumentSelector documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as",
            },
            new DocumentFilter()
            {
                Pattern = "**/*.asi",
            });

        private SynchronizationCapability? capability;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentSyncHandler"/> class.
        /// </summary>
        /// <param name="projectHost">The project host.</param>
        public DocumentSyncHandler(IWorkspaceHost projectHost)
        {
            this.projectHost = projectHost;
        }

        /// <summary>
        /// Gets the required change type.
        /// </summary>
        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        /// <inheritdoc/>
        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = documentSelector,
                SyncKind = Change,
            };
        }

        /// <inheritdoc/>
        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = documentSelector,
            };
        }

        /// <inheritdoc/>
        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = documentSelector,
                IncludeText = false,
            };
        }

        /// <inheritdoc/>
        public void SetCapability(SynchronizationCapability capability)
        {
            this.capability = capability;
        }

        /// <summary>
        /// Handles the notification of a document being opened by VS Code. When this happens, the file state should be switched
        /// to in-memory, rather than on-disk.
        /// </summary>
        /// <param name="notification">The document details.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Completion.</returns>
        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            if (notification is null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            projectHost.OpenFile(notification.TextDocument.Uri, notification.TextDocument.Text);

            return Unit.Task;
        }

        /// <summary>
        /// Handles the notification of a document being modified in-memory in VS Code. When this happens, the workspace in-memory state
        /// should be updated.
        /// </summary>
        /// <param name="notification">The document details.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Completion.</returns>
        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            if (notification is null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            projectHost.UpdateOpenFile(notification.TextDocument.Uri, notification.ContentChanges.First().Text);

            return Unit.Task;
        }

        /// <summary>
        /// Handles the notification of a document being closed in VS Code. When this happens, the workspace should
        /// switch this file to reading from disk.
        /// </summary>
        /// <param name="notification">The document details.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Completion.</returns>
        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            if (notification is null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            projectHost.CloseFile(notification.TextDocument.Uri);

            return Unit.Task;
        }

        /// <inheritdoc/>
        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Task;
        }

        /// <inheritdoc/>
        public TextDocumentAttributes? GetTextDocumentAttributes(Uri uri)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var fileExt = Path.GetExtension(uri.AbsolutePath);

            if (fileExt == ".asi")
            {
                return new TextDocumentAttributes(uri, "autostep-interaction");
            }
            else if (fileExt == ".as")
            {
                return new TextDocumentAttributes(uri, "autostep");
            }

            return null;
        }
    }
}
