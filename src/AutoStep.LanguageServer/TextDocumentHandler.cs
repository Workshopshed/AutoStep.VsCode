using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    class TextDocumentHandler : ITextDocumentSyncHandler
    {
        private readonly ILogger<TextDocumentHandler> logger;
        private readonly ILanguageServer langServer;
        private readonly IProjectHost projectHost;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as"
            },
            new DocumentFilter()
            {
                Pattern = "**/*.asi"
            }
        );

        private SynchronizationCapability _capability;

        public TextDocumentHandler(ILogger<TextDocumentHandler> logger, ILanguageServer server, IProjectHost projectHost)
        {
            this.logger = logger;
            langServer = server;
            this.projectHost = projectHost;
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change
            };
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = false
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            projectHost.OpenFile(notification.TextDocument.Uri, notification.TextDocument.Text);

            return Unit.Task;
        }


        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            projectHost.UpdateOpenFile(notification.TextDocument.Uri, notification.ContentChanges.First().Text);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            projectHost.CloseFile(notification.TextDocument.Uri);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Task;
        }

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            var fileExt = Path.GetExtension(uri.AbsolutePath);

            if(fileExt == ".asi")
            {
                return new TextDocumentAttributes(uri, "autostep-interaction");
            }
            else
            {
                return new TextDocumentAttributes(uri, "autostep");
            }
        }
    }

    class DidChangeWatchedFilesHandler : IDidChangeWatchedFilesHandler
    {
        private readonly IProjectHost projectHost;
        private DidChangeWatchedFilesCapability _capability;

        public DidChangeWatchedFilesHandler(IProjectHost projectHost)
        {
            this.projectHost = projectHost;
            _capability = new DidChangeWatchedFilesCapability();
        }

        public object GetRegistrationOptions()
        {
            return new object();
        }

        public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            foreach (var change in request.Changes)
            {
                switch (change.Type)
                {
                    case FileChangeType.Created:
                    {
                        projectHost.FileCreatedOnDisk(change.Uri);
                        break;
                    }

                    case FileChangeType.Changed:
                    {
                        projectHost.FileChangedOnDisk(change.Uri);
                        break;
                    }

                    case FileChangeType.Deleted:
                    {
                        projectHost.FileDeletedOnDisk(change.Uri);
                        break;
                    }
                };
            }

            projectHost.RequestBuild();

            return Unit.Task;
        }

        public void SetCapability(DidChangeWatchedFilesCapability capability)
        {
            _capability = capability;
        }
    }
}
