using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Composing;

namespace uSync.Publisher.Static
{
    public class SyncStaticDeployerCollection
        : BuilderCollectionBase<ISyncStaticDeployer>
    {
        public SyncStaticDeployerCollection(IEnumerable<ISyncStaticDeployer> items) 
            : base(items) { }

        public ISyncStaticDeployer GetDeployer(string alias)
            => this.FirstOrDefault(
                x => x.Alias.Equals(alias, StringComparison.InvariantCultureIgnoreCase));
    }

    public class SyncStaticDeployerCollectionBuilder
        : LazyCollectionBuilderBase<SyncStaticDeployerCollectionBuilder,
            SyncStaticDeployerCollection, ISyncStaticDeployer>
    {
        protected override SyncStaticDeployerCollectionBuilder This => this;
    }
}
