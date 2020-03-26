using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using AutoStep.Definitions;
using AutoStep.Elements;
using AutoStep.Elements.Parts;
using AutoStep.Elements.Test;
using AutoStep.Language.Position;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    public class TestCompletionHandler : StepReferenceAccessHandler, ICompletionHandler
    {
        private readonly DocumentSelector documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as"
            }
        );

        private CompletionCapability clientCapability;

        public TestCompletionHandler(IProjectHost projectHost) : base(projectHost)
        {
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = documentSelector
            };
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var pos = GetPositionInfo(request.TextDocument, request.Position);

            CompletionList completionList = null;

            if (pos is PositionInfo position)
            {
                if (TryGetStepReference(position, out var stepRef))
                {
                    // We are in a step reference.
                    // How much declaration do we have already?
                    var possibleMatches = ProjectHost.Project.Compiler.GetPossibleStepDefinitions(stepRef);

                    completionList = new CompletionList(possibleMatches.Select(m => new CompletionItem
                    {
                        Label = m.Definition.Declaration,
                        Kind = CompletionItemKind.Snippet,
                        Documentation = m.Definition.Definition.Description,
                        InsertText = GetCompletionString(m.Definition, out var fmt),
                        InsertTextFormat = fmt,
                        Preselect = m.IsExact,
                    }), false);

                    return Task.FromResult(completionList);
                }
                else if ((position.CurrentScope is ScenarioElement || position.CurrentScope is StepDefinitionElement) && position.LineTokens.Count == 0)
                {
                    // In a scenario or a step def; no other tokens on the line. Start a step reference.
                    
                    completionList = new CompletionList(new[]
                    {
                        new CompletionItem { Label = "Given ", Kind = CompletionItemKind.Keyword },
                        new CompletionItem { Label = "When ", Kind = CompletionItemKind.Keyword },
                        new CompletionItem { Label = "Then ", Kind = CompletionItemKind.Keyword },
                        new CompletionItem { Label = "And ", Kind = CompletionItemKind.Keyword }
                    }, true);
                }
            }

            return Task.FromResult(completionList);
        }

        private string GetCompletionString(StepDefinition definition, out InsertTextFormat format)
        {
            // Work out the total length.
            var snippetLength = 0;

            const int placeholderBaseCharacterCount = 4;
            const string placeholderPrefix = "${";
            const string placeholderSeparator = ":";
            const string placeholderTerminator = "}";

            int argNumber = 0;

            if(definition.Definition.Arguments.Count == 0)
            {
                format = InsertTextFormat.PlainText;
                return definition.Definition.Declaration;
            }

            var parts = definition.Definition.Parts;

            // Each argument adds the extra characters necessary to create placeholders.
            for (int idx = 0; idx < parts.Count; idx++)
            {
                var part = parts[idx];

                if (part is ArgumentPart arg)
                {
                    argNumber++;
                    snippetLength += placeholderBaseCharacterCount + argNumber.ToString().Length + arg.Name.Length;
                }
                else
                {
                    // Include the space.
                    snippetLength += part.Text.Length;
                }

                if (idx < parts.Count - 1)
                {
                    snippetLength++;
                }
            }

            var createdStr = string.Create(snippetLength, parts, (span, partSet) =>
            {
                int argNumber = 0;

                for (int idx = 0; idx < partSet.Count; idx++)
                {
                    var part = partSet[idx];
                    
                    if (part is ArgumentPart arg)
                    {
                        argNumber++;
                        var argNumberStr = argNumber.ToString();

                        placeholderPrefix.AsSpan().CopyTo(span);
                        span = span.Slice(placeholderPrefix.Length);

                        argNumberStr.AsSpan().CopyTo(span);
                        span = span.Slice(argNumberStr.Length);

                        placeholderSeparator.AsSpan().CopyTo(span);
                        span = span.Slice(placeholderSeparator.Length);

                        arg.Name.AsSpan().CopyTo(span);
                        span = span.Slice(arg.Name.Length);

                        placeholderTerminator.AsSpan().CopyTo(span);
                        span = span.Slice(placeholderTerminator.Length);
                    }
                    else
                    {
                        part.Text.AsSpan().CopyTo(span);
                        span = span.Slice(part.Text.Length);
                    }

                    if(idx < partSet.Count - 1)
                    {
                        span[0] = ' ';
                        span = span.Slice(1);
                    }
                }
            });

            format = InsertTextFormat.Snippet;
            return createdStr;
        }

        public void SetCapability(CompletionCapability capability)
        {
            clientCapability = capability;            
        }
    }
}
