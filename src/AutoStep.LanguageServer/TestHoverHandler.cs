using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    public class TestHoverHandler : StepReferenceAccessHandler, IHoverHandler
    {
        private HoverCapability capability;

        public TestHoverHandler(IProjectHost projectHost)
            : base(projectHost)
        {
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions { DocumentSelector = DocumentSelector };
        }

        public Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if(TryGetStepReference(request.TextDocument, request.Position, out var stepRef))
            {
                var stepDef = GetStepDefinition(stepRef);

                if(stepDef is object)
                {
                    var definitionDescription = stepDef.Definition.Description;

                    if (!string.IsNullOrEmpty(definitionDescription))
                    {
                        return Task.FromResult(new Hover
                        {
                            Contents = new MarkedStringsOrMarkupContent(definitionDescription),
                            Range = new Range(new Position(stepRef.SourceLine, stepRef.StartColumn), new Position(stepRef.SourceLine, stepRef.EndColumn))
                        });
                    }
                }
            }

            return Task.FromResult<Hover>(null);
        }

        public void SetCapability(HoverCapability capability)
        {
            this.capability = capability;
        }
    }
}
