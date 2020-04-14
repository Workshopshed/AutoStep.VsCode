using AutoStep.Elements;
using AutoStep.Language.Position;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Provides extension methods for AutoStep elements tor retrieve language server structures for position data.
    /// </summary>
    public static class ElementExtensions
    {
        /// <summary>
        /// Gets the range of an element.
        /// </summary>
        /// <param name="element">The autostep element.</param>
        /// <returns>The element range.</returns>
        public static Range Range(this PositionalElement element)
        {
            return new Range(element.Start(), element.End());
        }

        /// <summary>
        /// Gets the start position of the element.
        /// </summary>
        /// <param name="element">The autostep element.</param>
        /// <returns>The element start position.</returns>
        public static Position Start(this PositionalElement element)
        {
            if (element is null)
            {
                throw new System.ArgumentNullException(nameof(element));
            }

            return new Position(element.SourceLine - 1, element.StartColumn - 1);
        }

        /// <summary>
        /// Gets the end position of the element.
        /// </summary>
        /// <param name="element">The autostep element.</param>
        /// <returns>The element end position.</returns>
        public static Position End(this PositionalElement element)
        {
            if (element is null)
            {
                throw new System.ArgumentNullException(nameof(element));
            }

            return new Position(element.EndLine - 1, element.EndColumn);
        }

        /// <summary>
        /// Gets the start position of a position index line token.
        /// </summary>
        /// <param name="token">The positioned line token.</param>
        /// <param name="line">The line number.</param>
        /// <returns>The element start position.</returns>
        public static Position Start(this PositionLineToken token, long line)
        {
            if (token is null)
            {
                throw new System.ArgumentNullException(nameof(token));
            }

            return new Position(line, token.StartColumn - 1);
        }

        /// <summary>
        /// Gets the end position of a position index line token.
        /// </summary>
        /// <param name="token">The positioned line token.</param>
        /// <param name="line">The line number.</param>
        /// <returns>The element end position.</returns>
        public static Position End(this PositionLineToken token, long line)
        {
            if (token is null)
            {
                throw new System.ArgumentNullException(nameof(token));
            }

            return new Position(line, token.EndColumn);
        }
    }
}
