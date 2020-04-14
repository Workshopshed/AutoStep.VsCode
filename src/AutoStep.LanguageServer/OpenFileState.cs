#nullable disable

using System;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Represents the state of an open file in the workspace.
    /// </summary>
    internal class OpenFileState
    {
        /// <summary>
        /// Gets or sets the in-memory file content.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the UTC time at which the content was last modified.
        /// </summary>
        public DateTime LastModifyTime { get; set; }
    }
}
