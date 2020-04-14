using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions;
using AutoStep.Elements.Test;
using AutoStep.Language.Position;
using AutoStep.Projects;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Base class for handlers that need to access step reference details.
    /// </summary>
    public abstract class StepReferenceAccessHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StepReferenceAccessHandler"/> class.
        /// </summary>
        /// <param name="workspaceHost">The workspace host.</param>
        public StepReferenceAccessHandler(IWorkspaceHost workspaceHost)
        {
            WorkspaceHost = workspaceHost;
        }

        /// <summary>
        /// Gets the workspace host.
        /// </summary>
        protected IWorkspaceHost WorkspaceHost { get; }

        /// <summary>
        /// Gets the default test document selector.
        /// </summary>
        protected DocumentSelector DocumentSelector { get; } = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as",
            });

        /// <summary>
        /// Gets the position information for a given text document and position. Waits for an up-to-date build before returning.
        /// </summary>
        /// <param name="textDocument">The text document.</param>
        /// <param name="position">The position in the document.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A position block (if available).</returns>
        protected async Task<PositionInfo?> GetPositionInfoAsync(TextDocumentIdentifier textDocument, Position position, CancellationToken token)
        {
            if (textDocument is null)
            {
                throw new System.ArgumentNullException(nameof(textDocument));
            }

            if (position is null)
            {
                throw new System.ArgumentNullException(nameof(position));
            }

            await WorkspaceHost.WaitForUpToDateBuild(token);

            if (WorkspaceHost.TryGetOpenFile(textDocument.Uri, out var file) && file is ProjectTestFile testFile)
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

        /// <summary>
        /// Retrieves a step reference from the current scope.
        /// </summary>
        /// <param name="pos">The position information.</param>
        /// <param name="stepRef">The step reference.</param>
        /// <returns>True if a step reference was present at the provided position.</returns>
        protected static bool TryGetStepReference(PositionInfo? pos, [NotNullWhen(true)] out StepReferenceElement? stepRef)
        {
            if (pos?.CurrentScope is StepReferenceElement reference)
            {
                stepRef = reference;
                return true;
            }

            stepRef = null;
            return false;
        }

        /// <summary>
        /// Retrieves a step reference element for a given document and position.
        /// </summary>
        /// <param name="textDocument">The text document.</param>
        /// <param name="position">The position.</param>
        /// <param name="cancelToken">Cancellation token.</param>
        /// <returns>A task containing a found step reference, or null if no such reference is present.</returns>
        protected async Task<StepReferenceElement?> GetStepReferenceAsync(TextDocumentIdentifier textDocument, Position position, CancellationToken cancelToken)
        {
            var pos = await GetPositionInfoAsync(textDocument, position, cancelToken);

            if (TryGetStepReference(pos, out var stepRef))
            {
                return stepRef;
            }

            return null;
        }

        /// <summary>
        /// Get the step definition bound to a step reference (or null if not bound).
        /// </summary>
        /// <param name="reference">The step reference.</param>
        /// <returns>The bound step definition (if there is one).</returns>
        protected static StepDefinition? GetStepDefinition(StepReferenceElement reference)
        {
            if (reference is null)
            {
                throw new System.ArgumentNullException(nameof(reference));
            }

            return reference.Binding?.Definition;
        }
    }
}
