using AutoStep.Definitions.Test;
using AutoStep.Elements.Interaction;
using AutoStep.Execution.Contexts;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    public class TestDefinitionHandler : StepReferenceAccessHandler, IDefinitionHandler
    {
        private DefinitionCapability capability;

        public TestDefinitionHandler(IProjectHost projectHost) 
            : base(projectHost)
        {
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = DocumentSelector
            };
        }

        public Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            if(TryGetStepReference(request.TextDocument, request.Position, out var stepRef))
            {
                var stepDef = GetStepDefinition(stepRef);

                if(stepDef is object)
                {
                    Uri fileUid;

                    if(stepDef.Definition is InteractionStepDefinitionElement interactionDef)
                    {
                        fileUid = ProjectHost.GetPathUri(interactionDef.SourceName);
                    }
                    else if(stepDef.Source is FileStepDefinitionSource fileSource)
                    {
                        // Bound to be a file, just use the source ID.
                        fileUid = ProjectHost.GetPathUri(fileSource.File.Path);
                    }
                    else
                    {
                        // Cannot do anything here (yet). Must be a custom registration.
                        return Task.FromResult<LocationOrLocationLinks>(null);
                    }

                    var defElement = stepDef.Definition;

                    return Task.FromResult( new LocationOrLocationLinks(new LocationOrLocationLink(new Location { Uri = fileUid, Range = defElement.Range() })));
                }
            }

            return Task.FromResult<LocationOrLocationLinks>(null);
        }

        public void SetCapability(DefinitionCapability capability)
        {
            this.capability = capability;
        }
    }
}
