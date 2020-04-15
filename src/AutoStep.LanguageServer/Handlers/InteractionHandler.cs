using AutoStep.Definitions.Interaction;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Base interaction handler class.
    /// </summary>
    public abstract class InteractionHandler : BaseHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InteractionHandler"/> class.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        protected InteractionHandler(IWorkspaceHost workspace)
            : base(workspace)
        {
        }

        /// <summary>
        /// Gets the default interaction document selector.
        /// </summary>
        protected DocumentSelector DocumentSelector { get; } = new DocumentSelector(new DocumentFilter()
        {
            Pattern = "**/*.asi",
        });

        /// <summary>
        /// Get the method documentation for a given interaction method.
        /// </summary>
        /// <param name="methodDef">The method definition.</param>
        /// <returns>The string content (or null if no documentation is available).</returns>
        protected static string? GetMethodDocumentation(InteractionMethod? methodDef)
        {
            InteractionMethod? activeMethod = methodDef;

            string? documentation = null;

            // Use the most derived method definition that has a documentation block.
            while (string.IsNullOrWhiteSpace(documentation) && activeMethod is object)
            {
                documentation = activeMethod.GetDocumentation();

                if (activeMethod is FileDefinedInteractionMethod definedMethod)
                {
                    activeMethod = definedMethod.OverriddenMethod;
                }
                else
                {
                    activeMethod = null;
                }
            }

            return documentation;
        }
    }
}
