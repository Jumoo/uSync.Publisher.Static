using System.Collections.Generic;
using Umbraco.Core.Composing;

namespace uSync.Publisher.Static.Extensions
{
    public class StaticPublisherExtensionCollection
        : BuilderCollectionBase<IStaticPublisherExtension>
    {
        public StaticPublisherExtensionCollection(IEnumerable<IStaticPublisherExtension> items)
            : base(items) { }
    }

    public class StaticPublisherExtensionCollectionBuilder : OrderedCollectionBuilderBase<StaticPublisherExtensionCollectionBuilder, StaticPublisherExtensionCollection, IStaticPublisherExtension>
    {
        protected override StaticPublisherExtensionCollectionBuilder This => this;
    }
}
