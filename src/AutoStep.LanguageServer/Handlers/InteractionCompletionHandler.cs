using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions.Interaction;
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
    /// <summary>
    /// Intellisense completion handler for test files.
    /// </summary>
    public class InteractionCompletionHandler : InteractionHandler, ICompletionHandler
    {
        private CompletionCapability? clientCapability;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractionCompletionHandler"/> class.
        /// </summary>
        /// <param name="projectHost">The project host.</param>
        public InteractionCompletionHandler(IWorkspaceHost projectHost)
            : base(projectHost)
        {
        }

        /// <inheritdoc/>
        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = DocumentSelector,
            };
        }

        /// <summary>
        /// Handles the completion request.
        /// </summary>
        /// <param name="request">Request details (contains document and position info).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completion result.</returns>
        public async Task<CompletionList?> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var pos = await GetPositionInfoAsync(request.TextDocument, request.Position, cancellationToken);

            CompletionList? completionList = null;

            if (pos is PositionInfo position)
            {
                if (position.CurrentScope is MethodDefinitionElement ||
                    position.CurrentScope is InteractionStepDefinitionElement ||
                    (position.CurrentScope is MethodCallElement && CurrentOrPrecedingTokenIsMethodName(position)))
                {
                    // Show available method calls.
                    var container = position.Scopes.OfType<InteractionDefinitionElement>().FirstOrDefault();

                    if (container is object)
                    {
                        var methodTable = Workspace.GetMethodTableForInteractionDefinition(container);

                        if (methodTable is object)
                        {
                            var labelBuilder = new StringBuilder();

                            completionList = new CompletionList(methodTable.Methods.Select(m => new CompletionItem
                            {
                                Label = m.Key,
                                Detail = GetMethodDetail(m.Value, labelBuilder),
                                Kind = CompletionItemKind.Method,
                                Documentation = new MarkupContent { Value = GetMethodDocumentation(m.Value), Kind = MarkupKind.Markdown },
                            }));
                        }
                    }
                }
            }

            return completionList;
        }

        private static bool CurrentOrPrecedingTokenIsMethodName(PositionInfo pos)
        {
            var index = pos.CursorTokenIndex ?? pos.ClosestPrecedingTokenIndex;

            if (index.HasValue)
            {
                var token = pos.LineTokens[index.Value];

                return token.Category == LineTokenCategory.InteractionName &&
                       token.SubCategory == LineTokenSubCategory.InteractionMethod;
            }

            return false;
        }

        private string GetMethodDetail(InteractionMethod method, StringBuilder labelBuilder)
        {
            labelBuilder.Clear();

            labelBuilder.Append(method.Name);

            labelBuilder.Append('(');

            if (method is FileDefinedInteractionMethod fileMethod)
            {
                for (int idx = 0; idx < fileMethod.MethodDefinition.Arguments.Count; idx++)
                {
                    var arg = fileMethod.MethodDefinition.Arguments[idx];

                    if (idx > 0)
                    {
                        labelBuilder.Append(", ");
                    }

                    labelBuilder.Append(arg.Name);
                }
            }
            else
            {
                for (int idx = 0; idx < method.ArgumentCount; idx++)
                {
                    if (idx > 0)
                    {
                        labelBuilder.Append(", ");
                    }

                    labelBuilder.Append("arg");
                    labelBuilder.Append(idx + 1);
                }
            }

            labelBuilder.Append(')');

            return labelBuilder.ToString();
        }

        /// <inheritdoc/>
        public void SetCapability(CompletionCapability capability)
        {
            clientCapability = capability;
        }
    }
}
