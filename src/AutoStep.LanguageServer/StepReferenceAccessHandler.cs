using AutoStep.Definitions;
using AutoStep.Elements;
using AutoStep.Elements.Test;
using AutoStep.Language.Position;
using AutoStep.Projects;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AutoStep.LanguageServer
{
    public abstract class StepReferenceAccessHandler
    {
        public StepReferenceAccessHandler(IProjectHost projectHost)
        {
            ProjectHost = projectHost;
        }

        protected IProjectHost ProjectHost { get; }

        protected DocumentSelector DocumentSelector { get; } = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as"
            }
        );

        protected PositionInfo? GetPositionInfo(TextDocumentIdentifier textDocument, Position position)
        {
            if (ProjectHost.TryGetOpenFile(textDocument.Uri, out var file) && file is ProjectTestFile testFile)
            {
                var compileResult = testFile.LastCompileResult;

                if (compileResult?.Positions is object)
                {
                    // Lines and columns are zero-based in vscode, but 1-based in AutoStep.
                    return compileResult.Positions.Lookup(position.Line + 1, position.Character + 1);
                }
            }

            return null;
        }

        protected bool TryGetStepReference(PositionInfo? pos, out StepReferenceElement stepRef)
        {
            if (pos?.CurrentScope is StepReferenceElement reference)
            {
                stepRef = reference;
                return true;
            }

            stepRef = null;
            return false;
        }


        protected bool TryGetStepReference(TextDocumentIdentifier textDocument, Position position, out StepReferenceElement stepRef)
        {
            var pos = GetPositionInfo(textDocument, position);

            return TryGetStepReference(pos, out stepRef);
        }

        protected StepDefinition GetStepDefinition(StepReferenceElement reference)
        {
            return reference.Binding?.Definition;
        }
    }
}
