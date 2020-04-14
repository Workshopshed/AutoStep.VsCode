using System;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions.Test;
using AutoStep.Elements.Interaction;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Handles requests to get the definitiof a step reference.
    /// </summary>
    public class TestDefinitionHandler : StepReferenceAccessHandler, IDefinitionHandler
    {
        private DefinitionCapability? capability;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDefinitionHandler"/> class.
        /// </summary>
        /// <param name="projectHost">The project host.</param>
        public TestDefinitionHandler(IWorkspaceHost projectHost)
            : base(projectHost)
        {
        }

        /// <inheritdoc/>
        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = DocumentSelector,
            };
        }

        /// <inheritdoc/>
        public async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var stepRef = await GetStepReferenceAsync(request.TextDocument, request.Position, cancellationToken);

            if (stepRef is object)
            {
                var stepDef = GetStepDefinition(stepRef);

                if (stepDef is object && stepDef.Definition is object)
                {
                    Uri? fileUid;

                    if (stepDef.Definition is InteractionStepDefinitionElement interactionDef)
                    {
                        fileUid = WorkspaceHost.GetPathUri(interactionDef.SourceName ?? string.Empty);
                    }
                    else if (stepDef.Source is FileStepDefinitionSource fileSource)
                    {
                        // Bound to be a file, just use the source ID.
                        fileUid = WorkspaceHost.GetPathUri(fileSource.File.Path);
                    }
                    else
                    {
                        // Cannot do anything here (yet). Must be a custom registration.
                        return null;
                    }

                    var defElement = stepDef.Definition;

                    return new LocationOrLocationLinks(new LocationOrLocationLink(new Location { Uri = fileUid, Range = defElement.Range() }));
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public void SetCapability(DefinitionCapability capability)
        {
            this.capability = capability;
        }
    }
}
