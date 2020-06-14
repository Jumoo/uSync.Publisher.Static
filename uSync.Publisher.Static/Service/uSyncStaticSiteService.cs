using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Services;
using Umbraco.Web;

using uSync.Expansions.Core.Physical;
using uSync.Publisher.Configuration;

using uSync8.BackOffice.Services;
using uSync8.BackOffice.SyncHandlers;
using uSync8.Core.Extensions;
using System.ComponentModel.DataAnnotations;

namespace uSync.Publisher.Static
{
    public class uSyncStaticSiteService
    {
        private readonly UmbracoHelper helper;
        private readonly IEntityService entityService;
        private readonly IContentService contentService;
        private readonly SyncFileService syncFileService;
        private readonly uSyncMediaFileService mediaFileService;
        private readonly IUmbracoContextFactory contextFactory;
        private readonly TemplateFileService templateFileService;
        private readonly SyncStaticDeployerCollection deployers;

        private readonly string syncRoot;
        private readonly string configFile;

        private readonly IProfilingLogger logger;

        public uSyncStaticSiteService(UmbracoHelper helper,
            IEntityService entityService,
            IContentService contentService,
            SyncFileService syncFileService,
            uSyncMediaFileService mediaFileService,
            TemplateFileService templateFileService,
            IUmbracoContextFactory contextFactory,
            IProfilingLogger logger,
            uSyncPublisherConfig config,
            SyncStaticDeployerCollection deployers,
            IGlobalSettings settings)
        {
            this.helper = helper;
            this.entityService = entityService;

            this.syncFileService = syncFileService;
            this.mediaFileService = mediaFileService;
            this.templateFileService = templateFileService;

            this.contentService = contentService;
            this.contextFactory = contextFactory;

            this.syncRoot = Path.GetFullPath(Path.Combine(settings.LocalTempPath, "uSync", "pack"));
            this.configFile = Path.Combine(Umbraco.Core.IO.SystemDirectories.Config + "/uSync.Publish.config");

            this.deployers = deployers;

            this.logger = logger;
        }

        public string GenerateItemHtml(Udi udi)
        {
            var id = GetItemId(udi);
            if (id > 0)
                return GenerateItemHtml(id);

            return "Item not found";
        }

        public string GenerateItemHtml(int id)
        {
            return helper.RenderTemplate(id).ToString();
        }

        public string GetItemPath(int id)
        {
            using (var context = contextFactory.EnsureUmbracoContext())
            {
                var item = context.UmbracoContext.Content.GetById(id);
                if (item != null)
                {
                    return item.Url;
                }
                return $"_errors/{id}/";
            }
        }

        public bool SaveHtml(Guid packId, int itemId)
        {
            try
            {
                var item = contentService.GetById(itemId);
                if (item != null && item.Published)
                {
                    var html = GenerateItemHtml(itemId);
                    var path = GetItemPath(itemId);
                    var filePath = $"{syncRoot}/{packId}/{path}/index.html".Replace("/", "\\");
                    syncFileService.SaveFile(filePath, html);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Warn<uSyncStaticSiteService>("Error Saving Html", ex);
            }

            return false;
        }

        public bool SaveMedia(Guid packId, Udi mediaId)
        {
            var path = $"{syncRoot}/{packId}";
            mediaFileService.CopyMediaFile(mediaId, path);
            return true;
        }

        public bool SaveFolders(Guid id, params string[] folders)
        {
            var packFolder = $"{syncRoot}/{id}";

            foreach (var folder in folders)
            {
                var path = Path.Combine(packFolder, folder.Substring(2));
                templateFileService.CopyFolder(folder, path);
            }
            return true;
        }

        public Attempt<int> Publish(Guid id, string server, SyncUpdateCallback updateCallback)
        {

            var source = $"{syncRoot}\\{id}";

            var deployConfig = LoadDeployerConfig(server);
            if (deployConfig == null) throw new ArgumentException("No deploy config for server", nameof(server));

            var deployerAlias = deployConfig.Attribute("alias").ValueOrDefault(string.Empty);
            if (string.IsNullOrWhiteSpace(deployerAlias)) throw new ArgumentException("No deployer alias in config", nameof(server));

            var deployer = deployers.GetDeployer(deployerAlias);
            if (deployer != null)
            {
                return deployer.Deploy(source, deployConfig, updateCallback);
            }

            return Attempt.Fail(0);
        }

        private XElement LoadDeployerConfig(string serverAlias)
        {

            var serverConfig = LoadServerConfig(serverAlias);
            if (serverConfig != null)
            {
                return serverConfig.Element("deployer");
            }

            return null;
        }

        internal XElement LoadServerConfig(string serverAlias)
        {
            var settingsFile = IOHelper.MapPath(this.configFile);
            var node = XElement.Load(settingsFile);

            var servers = node.Elements("servers");
            if (servers != null)
            {
                var serverNode = servers.Elements()
                    .Where(x => x.Attribute("alias").ValueOrDefault(string.Empty).InvariantEquals(serverAlias))
                    .FirstOrDefault();

                if (serverAlias != null)
                    return serverNode;
            }

            return null;
        }

        public int GetItemId(Udi udi)
        {
            if (udi is GuidUdi guidUdi)
            {
                var item = entityService.Get(guidUdi.Guid);
                if (item != null)
                {
                    return item.Id;
                }
            }

            return -1;
        }
    }
}
