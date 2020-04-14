using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions;
using AutoStep.Definitions.Interaction;
using AutoStep.Elements;
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    public abstract class InteractionHandler : BaseHandler
    {
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
    }
}
