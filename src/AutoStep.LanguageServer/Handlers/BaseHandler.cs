using System.Threading;
using System.Threading.Tasks;
using AutoStep.Language.Position;
using AutoStep.Projects;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Base class for all AutoStep handlers.
    /// </summary>
    public abstract class BaseHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseHandler"/> class.
        /// </summary>
        /// <param name="workspaceHost">The workspace host.</param>
        public BaseHandler(IWorkspaceHost workspaceHost)
        {
            Workspace = workspaceHost;
        }

        /// <summary>
        /// Gets the workspace host.
        /// </summary>
        public IWorkspaceHost Workspace { get; }

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

            await Workspace.WaitForUpToDateBuild(token);

            if (Workspace.TryGetOpenFile(textDocument.Uri, out var file))
            {
                IPositionIndex? posIndex = null;

                if (file is ProjectTestFile testFile)
                {
                    var compileResult = testFile.LastCompileResult;

                    posIndex = compileResult?.Positions;
                }
                else if (file is ProjectInteractionFile interactionFile)
                {
                    var compileResult = interactionFile.LastCompileResult;

                    posIndex = compileResult?.Positions;
                }

                if (posIndex is object)
                {
                    // Lines and columns are zero-based in vscode, but 1-based in AutoStep.
                    return posIndex.Lookup(position.Line + 1, position.Character + 1);
                }
            }

            return null;
        }
    }
}
