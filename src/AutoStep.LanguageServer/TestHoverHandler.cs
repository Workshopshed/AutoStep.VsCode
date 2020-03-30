using AutoStep.Definitions;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using Microsoft.Extensions.Primitives;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    public class TestHoverHandler : StepReferenceAccessHandler, IHoverHandler
    {
        private bool supportsMarkdown;

        public TestHoverHandler(IProjectHost projectHost)
            : base(projectHost)
        {
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions { DocumentSelector = DocumentSelector };
        }

        public async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var stepRef = await GetStepReferenceAsync(request.TextDocument, request.Position, cancellationToken);

            if (stepRef is object)
            {
                var stepDef = GetStepDefinition(stepRef);

                if(stepDef is object)
                {
                    return GetHoverResult(stepRef, stepDef);
                }
            }

            return null;
        }

        private Hover GetHoverResult(StepReferenceElement stepRef, StepDefinition stepDef)
        {
            var definitionDescription = stepDef.Definition.Description;

            // Include argument values in the description.
            if (stepRef.Binding.Arguments.Length > 0)
            {
                var builder = new StringBuilder(definitionDescription);

                if (!string.IsNullOrWhiteSpace(definitionDescription))
                {
                    builder.AppendLine(supportsMarkdown ? "  " : string.Empty);
                }

                if (supportsMarkdown)
                {
                    builder.AppendLine("**Arguments**  ");
                }
                else
                {
                    builder.AppendLine("Arguments");
                }

                foreach (var arg in stepRef.Binding.Arguments)
                {
                    builder.Append(arg.ArgumentName);
                    builder.Append(": ");
                    builder.Append(arg.GetRawText(stepRef.RawText));
                    builder.AppendLine("  ");
                }

                definitionDescription = builder.ToString();
            }

            if (!string.IsNullOrEmpty(definitionDescription))
            {
                var markupContent = new MarkupContent();
                markupContent.Value = definitionDescription;
                markupContent.Kind = supportsMarkdown ? MarkupKind.Markdown : MarkupKind.PlainText;

                return new Hover
                {
                    Contents = new MarkedStringsOrMarkupContent(markupContent),
                    Range = new Range(new Position(stepRef.SourceLine, stepRef.StartColumn), new Position(stepRef.SourceLine, stepRef.EndColumn))
                };
            }

            return null;
        }

        public void SetCapability(HoverCapability capability)
        {
            this.supportsMarkdown = capability.ContentFormat.Contains(MarkupKind.Markdown);
        }
    }
}
