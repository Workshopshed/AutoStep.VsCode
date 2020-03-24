using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Threading.Tasks;

namespace AutoStep.LanguageServer
{
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
