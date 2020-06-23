using Umbraco.Core.Composing;
using uSync.Publisher.Static.Extensions;

namespace uSync.Publisher.Static
{
    public static class CompositionExtensions
    {
        /// <summary>
        /// Returns the collection builder for static publisher extensions
        /// </summary>
        /// <param name="composition">The composition from the composer method</param>
        /// <returns>The static publisher extension collection builder</returns>
        public static StaticPublisherExtensionCollectionBuilder StaticPublisherExtensions(this Composition composition) => composition.WithCollectionBuilder<StaticPublisherExtensionCollectionBuilder>();
    }
}
