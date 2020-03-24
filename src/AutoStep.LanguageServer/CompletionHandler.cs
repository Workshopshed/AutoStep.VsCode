using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    public class TestCompletionHandler : ICompletionHandler
    {
        private readonly DocumentSelector documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as"
            }
        );

        private CompletionCapability clientCapability;

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var completionList = new CompletionList();

            return Task.FromResult(completionList);
        }

        public void SetCapability(CompletionCapability capability)
        {
            clientCapability = capability;            
        }
    }
}
