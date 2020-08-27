using CodeFactory.DotNet.CSharp;
using CodeFactory.Formatting.CSharp;

namespace CoreAutomation.AspNet.Commands.ExplorerCommands.SourceCode.Template
{
    /// <summary>
    /// Data class used for adding member data through templates
    /// </summary>
    public class AddMemberData
    {
        /// <summary>
        /// Namespace manager use for updating type definitions.
        /// </summary>
        public NamespaceManager Manager { get; set; }

        /// <summary>
        /// The method to be processed by the template
        /// </summary>
        public CsMethod MethodModel { get; set; }

        /// <summary>
        /// The property to be processed by the template.
        /// </summary>
        public CsProperty PropertyModel { get; set; }

        /// <summary>
        /// The event to be processed by the template
        /// </summary>
        public CsEvent EventModel { get; set; }
    }
}
