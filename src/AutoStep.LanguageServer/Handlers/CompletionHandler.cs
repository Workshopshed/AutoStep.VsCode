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
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Parts;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using AutoStep.Language;
using AutoStep.Language.Position;
using AutoStep.Language.Test.Matching;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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
                DocumentSelector = documentSelector,
                TriggerCharacters = new[] { " " }
            };
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var pos = await GetPositionInfoAsync(request.TextDocument, request.Position, cancellationToken);

            CompletionList completionList = null;

            if (pos is PositionInfo position)
            {
                if (TryGetStepReference(position, out var stepRef))
                {
                    // We are in a step reference.
                    // How much declaration do we have already?
                    var possibleMatches = ProjectHost.ProjectContext.Project.Compiler.GetPossibleStepDefinitions(stepRef);

                    var startInsertPos = request.Position;
                    var endInsertPos = request.Position;

                    var firstInsertToken = position.LineTokens.FirstOrDefault(t => t.Category == LineTokenCategory.StepText || t.Category == LineTokenCategory.Variable);
                    var lastInsertToken = position.LineTokens.LastOrDefault(t => t.Category == LineTokenCategory.StepText || t.Category == LineTokenCategory.Variable);

                    if (firstInsertToken is object)
                    {
                        startInsertPos = firstInsertToken.Start(request.Position.Line);
                    }

                    if (lastInsertToken is object)
                    {
                        endInsertPos = lastInsertToken?.End(request.Position.Line);
                    }

                    completionList = new CompletionList(ExpandPlaceholders(possibleMatches).Select(m => new CompletionItem
                    {
                        Label = GetCompletionString(m, stepRef, CompletionStringMode.Label, out var _),
                        Kind = CompletionItemKind.Snippet,
                        Documentation = m.Match.Definition.Definition.Description,
                        FilterText = GetCompletionString(m, stepRef, CompletionStringMode.Filter, out var _),
                        TextEdit = new TextEdit
                        {
                            NewText = GetCompletionString(m, stepRef, CompletionStringMode.Snippet, out var fmt),
                            Range = new Range(startInsertPos, endInsertPos)
                        },
                        InsertTextFormat = fmt,
                        Preselect = m.Match.IsExact,
                    }), false);

                    return completionList;
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

            return completionList;
        }

        private IEnumerable<ExpandedMatch> ExpandPlaceholders(IEnumerable<IMatchResult> matches)
        {
            foreach (var match in matches)
            {
                if(match.Definition.Definition is InteractionStepDefinitionElement interactionDef && interactionDef.ValidComponents.Any())
                {
                    // There are some valid components at work.
                    // Expand them into individual matches.
                    // Do we have a component placeholder in the match set?
                    if(match.PlaceholderValues is object && match.PlaceholderValues.TryGetValue(StepPlaceholders.Component, out var placeholderValue))
                    {
                        // Just the one match (with the component).
                        yield return new ExpandedMatch(match, placeholderValue);
                    }
                    else
                    {
                        // Expand the match.
                        foreach (var validComponent in interactionDef.ValidComponents)
                        {
                            yield return new ExpandedMatch(match, validComponent);
                        }
                    }
                }
                else
                {
                    yield return new ExpandedMatch(match);
                }
            }
        }

        private struct ExpandedMatch
        {
            public ExpandedMatch(IMatchResult match, string componentName = null)
            {
                Match = match;
                ComponentName = componentName;
            }

            public IMatchResult Match { get; }

            public string ComponentName { get; }
        }

        private enum CompletionStringMode
        {
            Label,
            Snippet,
            Filter
        }

        private string GetCompletionString(ExpandedMatch match, StepReferenceElement stepRef, CompletionStringMode mode, out InsertTextFormat format)
        {
            // Work out the total length.
            var snippetLength = 0;

            const int placeholderBaseCharacterCount = 4;
            const string placeholderPrefix = "${";
            const string placeholderSeparator = ":";
            const string placeholderTerminator = "}";
            string newLine = Environment.NewLine;

            int argNumber = 0;

            var definition = match.Match.Definition.Definition;

            if(definition.Arguments.Count == 0 && !(definition is InteractionStepDefinitionElement intEl && intEl.ValidComponents.Any()))
            {
                format = InsertTextFormat.PlainText;
                return definition.Declaration;
            }

            var parts = definition.Parts;

            var startPartIdx = 0;

            format = InsertTextFormat.PlainText;

            // Each argument adds the extra characters necessary to create placeholders.
            for (int idx = startPartIdx; idx < parts.Count; idx++)
            {
                var part = parts[idx];

                if (part is ArgumentPart arg)
                {
                    // Do we know the value yet?
                    var knownArgValue = match.Match.Arguments.FirstOrDefault(a => a.ArgumentName == arg.Name);

                    if(knownArgValue is object)
                    {
                        snippetLength += knownArgValue.GetRawLength();

                        if(knownArgValue.StartExclusive)
                        {
                            snippetLength += 1;
                        }

                        if(knownArgValue.EndExclusive)
                        {
                            snippetLength += 1;
                        }
                    }
                    else if (mode == CompletionStringMode.Snippet)
                    {
                        argNumber++;
                        snippetLength += placeholderBaseCharacterCount + argNumber.ToString().Length + arg.Name.Length;
                    }
                    else
                    {
                        snippetLength += part.Text.Length;
                    }

                    format = InsertTextFormat.Snippet;
                }
                else if(part is PlaceholderMatchPart placeholder && placeholder.PlaceholderValueName == StepPlaceholders.Component)
                {
                    if(match.ComponentName is object)
                    {
                        snippetLength += match.ComponentName.Length;
                    }
                    else
                    {
                        snippetLength += part.Text.Length;
                    }
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

            snippetLength += newLine.Length;

            var createdStr = string.Create(snippetLength, (parts, match.ComponentName, stepRef, mode, startPartIdx), (span, m) =>
            {
                var partSet = m.parts;
                int argNumber = 0;

                for (int idx = m.startPartIdx; idx < partSet.Count; idx++)
                {
                    var part = partSet[idx];
                    
                    if (part is ArgumentPart arg)
                    {
                        // Do we know the value yet?
                        var knownArgValue = match.Match.Arguments.FirstOrDefault(a => a.ArgumentName == arg.Name);

                        if (knownArgValue is object)
                        {
                            if(knownArgValue.StartExclusive)
                            {
                                span[0] = '\'';
                                span = span.Slice(1);
                            }

                            var raw = knownArgValue.GetRawText(m.stepRef.RawText);
                            raw.AsSpan().CopyTo(span);
                            span = span.Slice(raw.Length);

                            if(knownArgValue.EndExclusive)
                            {
                                span[0] = '\'';
                                span = span.Slice(1);
                            }
                        }
                        else if(m.mode == CompletionStringMode.Snippet)
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
                    }
                    else if (part is PlaceholderMatchPart placeholder && placeholder.PlaceholderValueName == StepPlaceholders.Component)
                    {
                        m.ComponentName.AsSpan().CopyTo(span);
                        span = span.Slice(m.ComponentName.Length);
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

                newLine.AsSpan().CopyTo(span);
            });

            return createdStr;
        }

        public void SetCapability(CompletionCapability capability)
        {
            clientCapability = capability;            
        }
    }
}
