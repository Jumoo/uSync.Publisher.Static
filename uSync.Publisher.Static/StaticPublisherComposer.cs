using Umbraco.Core;
using Umbraco.Core.Composing;

namespace uSync.Publisher.Static
{
    [ComposeAfter(typeof(uSyncPublishComposer))]
    public class StaticPublisherComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.WithCollectionBuilder<SyncStaticDeployerCollectionBuilder>()
                .Add(() => composition.TypeLoader.GetTypes<ISyncStaticDeployer>());

            composition.RegisterUnique<IuSyncStaticSiteService, uSyncStaticSiteService>();
        }
    }
}
