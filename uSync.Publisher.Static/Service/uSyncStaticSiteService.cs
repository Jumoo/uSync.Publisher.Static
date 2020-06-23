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

using uSync8.BackOffice.Services;
using uSync8.BackOffice.SyncHandlers;
using uSync8.Core.Extensions;
using System.Collections.Generic;
using uSync.Publisher.Static.Extensions;
using uSync.Publisher.Publishers;
using uSync.Publisher.Services;
using uSync.Expansions.Core.Models;
using uSync8.BackOffice;
using uSync8.Core.Dependency;

namespace uSync.Publisher.Static
{
    public interface IuSyncStaticSiteService
    {
        /// <summary>
        /// Retrieves all dependencies which will be worked on by this push
        /// </summary>
        /// <param name="syncItems">The items that were requested to be pushed</param>
        /// <param name="callbacks">Any callbacks that will be called while calculating dependencies</param>
        /// <returns></returns>
        IEnumerable<uSyncDependency> GetDependencies(IEnumerable<SyncItem> syncItems, uSyncCallbacks callbacks);

        /// <summary>
        /// Retrieves the ID of an item, given its UDI
        /// </summary>
        /// <param name="udi">The UDI of the item</param>
        /// <returns>The ID of the item</returns>
        int GetItemId(Udi udi);

        /// <summary>
        /// Verifies that an item ID is valid to be saved, then saved the HTML of that content item to the staging area
        /// </summary>
        /// <param name="packId">The unique ID of the publish</param>
        /// <param name="itemId">The UDI of the item to be saved to disk</param>
        /// <param name="generatingCallback">A callback which should be called once it has been verified that an item is valid for saving, but before generating and saving the HTML</param>
        /// <returns>True if the file was generated and saved, false otherwise</returns>
        bool SaveHtml(Guid packId, Udi itemId, Action generatingCallback);

        /// <summary>
        /// Saves a media item to the staging area
        /// </summary>
        /// <param name="packId">The unique ID of the publish</param>
        /// <param name="mediaId">The UDI of the item to be saved</param>
        /// <returns>True if the file was saved, false otherwise</returns>
        bool SaveMedia(Guid packId, Udi mediaId);

        /// <summary>
        /// Copies the files and folders specified and saves any custom files to the staging area
        /// </summary>
        /// <param name="id">The unique ID of the publish</param>
        /// <param name="copyFolders">The folders which will be copied to the staging area</param>
        /// <param name="copyFiles">The files which will be copied to the staging area, with the key being the source file path, and the value being the destination</param>
        /// <param name="customFiles">The custom files to write out to the staging area</param>
        /// <returns>True if all files were written successfully, false otherwise</returns>
        bool SaveFilesAndFolders(Guid id, ICollection<string> copyFolders, IDictionary<string, string> copyFiles, IDictionary<string, Stream> customFiles);

        /// <summary>
        /// Deploys all content from the staging area to the destination server
        /// </summary>
        /// <param name="id">The unique ID of the publish</param>
        /// <param name="server">The server to publish to</param>
        /// <param name="updateCallback">Any update callbacks to call during the publishing process</param>
        /// <returns></returns>
        Attempt<int> Publish(Guid id, string server, SyncUpdateCallback updateCallback);

        /// <summary>
        /// Loads the server config, initializes any extensions, and returns the server config to the caller
        /// </summary>
        /// <param name="id">The unique ID of the publish</param>
        /// <param name="action">The action which was called</param>
        /// <param name="args">The arguments for the action currently being processed</param>
        /// <returns>The server config for the destination server</returns>
        XElement Initialize(Guid id, SyncPublisherAction action, ActionArguments args);

        /// <summary>
        /// Notifies extensions that a specific step has been completed
        /// </summary>
        /// <param name="stepName">The name of the step which was completed</param>
        void StepCompleted(string stepName);

        /// <summary>
        /// Notifies extensions that all processing for a push process has been completed
        /// </summary>
        void PushComplete();
    }

    public class uSyncStaticSiteService : IuSyncStaticSiteService
    {
        private readonly UmbracoHelper helper;
        private readonly IEntityService entityService;
        private readonly SyncFileService syncFileService;
        private readonly uSyncMediaFileService mediaFileService;
        private readonly IUmbracoContextFactory contextFactory;
        private readonly TemplateFileService templateFileService;
        private readonly SyncStaticDeployerCollection deployers;
        private readonly List<(IStaticPublisherExtension Extension, object State)> extensions;
        private readonly uSyncOutgoingService outgoingService;

        private readonly string syncRoot;
        private readonly string configFile;

        private readonly IProfilingLogger logger;

        public uSyncStaticSiteService(UmbracoHelper helper,
            IEntityService entityService,
            SyncFileService syncFileService,
            uSyncMediaFileService mediaFileService,
            TemplateFileService templateFileService,
            IUmbracoContextFactory contextFactory,
            IProfilingLogger logger,
            SyncStaticDeployerCollection deployers,
            IGlobalSettings settings,
            uSyncOutgoingService outgoingService,
            StaticPublisherExtensionCollection extensions)
        {
            this.helper = helper;
            this.entityService = entityService;

            this.syncFileService = syncFileService;
            this.mediaFileService = mediaFileService;
            this.templateFileService = templateFileService;

            this.contextFactory = contextFactory;

            this.syncRoot = Path.GetFullPath(Path.Combine(settings.LocalTempPath, "uSync", "pack"));
            this.configFile = Path.Combine(SystemDirectories.Config + "/uSync.Publish.config");

            this.deployers = deployers;
            this.outgoingService = outgoingService;
            this.extensions = extensions?.Select(e => (e, (object)null)).ToList() ?? new List<(IStaticPublisherExtension, object)>();

            this.logger = logger;
        }

        public IEnumerable<uSyncDependency> GetDependencies(IEnumerable<SyncItem> syncItems, uSyncCallbacks callbacks)
        {
            var dependencies = outgoingService.GetItemDependencies(syncItems, callbacks)?.ToList() ?? new List<uSyncDependency>();

            RunExtension((e, s) => e.TransformDependecies(s, dependencies));

            return dependencies;
        }

        public bool ShouldGenerateHtml(int id) => helper.Content(id) != null;

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

        public bool SaveHtml(Guid packId, Udi itemId, Action generatingCallback)
        {
            try
            {
                var id = GetItemId(itemId);
                var item = id > 0 ? helper.Content(id) : null;
                if (item == null) return false;

                generatingCallback?.Invoke();
                var html = GenerateItemHtml(id);
                var path = GetItemPath(id);

                RunExtension((e, s) => e.TransformHtml(s, item, ref path, ref html));
                if (html == null) return false;

                var filePath = $"{syncRoot}/{packId}/{path}/index.html".Replace("/", "\\");
                syncFileService.SaveFile(filePath, html);
                return true;
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
            RunExtension((e, s) => e.TransformMedia(s, mediaId, path));
            return true;
        }

        public bool SaveFilesAndFolders(Guid id, ICollection<string> copyFolders, IDictionary<string, string> copyFiles, IDictionary<string, Stream> customFiles)
        {
            var packFolder = $"{syncRoot}/{id}";

            if (copyFolders == null) copyFolders = new List<string>();
            if (copyFiles == null) copyFiles = new Dictionary<string, string>();
            if (customFiles == null) customFiles = new Dictionary<string, Stream>();

            RunExtension((e, s) => e.TransformCustomFilesAndFolders(s, copyFolders, copyFiles, customFiles));

            foreach (var folder in copyFolders)
            {
                var path = Path.Combine(packFolder, folder.StartsWith("~/") ? folder.Substring(2) : folder);
                templateFileService.CopyFolder(folder, path);
            }

            foreach (var srcDest in copyFiles)
            {
                var src = syncFileService.GetAbsPath(srcDest.Key);
                var dst = syncFileService.GetAbsPath(Path.Combine(packFolder, srcDest.Value.StartsWith("~/") ? srcDest.Value.Substring(2) : srcDest.Value));
                using (var str = File.OpenRead(src)) syncFileService.SaveFile(dst, str);
            }

            foreach (var dstStream in customFiles)
            {
                var dst = syncFileService.GetAbsPath(Path.Combine(packFolder, dstStream.Key.StartsWith("~/") ? dstStream.Key.Substring(2) : dstStream.Key));
                using (dstStream.Value) syncFileService.SaveFile(dst, dstStream.Value);
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

        public XElement LoadServerConfig(string serverAlias)
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

        public XElement Initialize(Guid id, SyncPublisherAction action, ActionArguments args)
        {
            var serverConfig = LoadServerConfig(args.Target);

            for (var i = 0; i < extensions.Count; i++)
            {
                var extension = extensions[i].Extension;
                var state = (object)null;

                try
                {
                    state = extension.Initialize(id, syncRoot, action, args, serverConfig);
                }
                catch(Exception ex)
                {
                    logger.Error(extension.GetType(), ex, "Could not initialize extension");
                }

                extensions[i] = (extension, state);
            }

            return serverConfig;
        }

        public void StepCompleted(string stepName) => RunExtension((e, s) => e.StepCompleted(s, stepName));

        public void PushComplete() => RunExtension((e, s) => e.PushComplete(s));

        private void RunExtension(Action<IStaticPublisherExtension, object> method)
        {
            foreach (var extension in extensions)
            {
                try
                {
                    method(extension.Extension, extension.State);
                }
                catch (Exception ex)
                {
                    logger.Error(extension.Extension.GetType(), "Could not run the extension", ex);
                }
            }
        }
    }
}
