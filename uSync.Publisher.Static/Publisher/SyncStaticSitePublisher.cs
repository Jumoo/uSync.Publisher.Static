using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using HtmlAgilityPack;

using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;

using uSync.Expansions.Core.Services;
using uSync.Publisher.Configuration;
using uSync.Publisher.Models;
using uSync.Publisher.Publishers;
using uSync.Publisher.Services;

using uSync8.BackOffice;
using uSync8.Core.Dependency;
using uSync8.Core.Extensions;

using static Umbraco.Core.Constants;

namespace uSync.Publisher.Static
{
    /// <summary>
    ///  Static file publisher, publishes the site to static files, 
    /// </summary>
    public class SyncStaticSitePublisher : SyncStepImportBase, IStepPublisher, IStepPublisherConfig
    {
        public const string CalculateStepName = "Calculate";
        public const string CreatePageStepName = "Creating Pages";
        public const string GatherMediaStepName = "Gathering Media";
        public const string FilesStepName = "Files";
        public const string UploadStepName = "Upload";

        public string Name => "Static Site Publisher";
        public string Alias => "static";

        public string ConfigView => uSyncPublisher.PluginFolder + "publishers/static/serverConfig.html";

        private readonly IuSyncStaticSiteService staticSiteService;

        public SyncStaticSitePublisher(uSyncPublisherConfig config,
            IProfilingLogger logger,
            IGlobalSettings settings,
            uSyncIncomingService incomingService,
            IuSyncStaticSiteService staticSiteService)
            : base(config, logger, settings, incomingService)
        {
            this.staticSiteService = staticSiteService;
            
            Actions = new Dictionary<PublishMode, IEnumerable<SyncPublisherAction>>()
            {
                { PublishMode.Push, PushActions }
            };
        }

        public override Task<SyncServerStatus> GetStatus(string target)
        {
            return Task.FromResult(SyncServerStatus.Success);
        }

        public IEnumerable<SyncPublisherAction> PushActions => new List<SyncPublisherAction>()
        {
            new SyncPublisherAction("config", "Publish",
                new SyncPublisherStep("Publish Site", "icon-document"), Start)
            {
                View = uSyncPublisher.PluginFolder + "publishers/static/config.html",
                ButtonText = "Publish Site"
            },
            new SyncPublisherAction("Publish", "Publish Files", Publish)
            {
                Steps = new List<SyncPublisherStep>()
                {
                    new SyncPublisherStep(CalculateStepName, "icon-science", "Calculating"),
                    new SyncPublisherStep(CreatePageStepName, "icon-box", "Files"),
                    new SyncPublisherStep(GatherMediaStepName, "icon-picture", "Media"),
                    new SyncPublisherStep(FilesStepName, "icon-documents", "Files"),
                    new SyncPublisherStep(UploadStepName, "icon-truck usync-truck", "Uploading")
                }
            },
            new SyncPublisherAction("result", "Publish Results", new SyncPublisherStep("Publish", "icon-result"), Complete)
            {
                View = uSyncPublisher.PluginFolder + "publishers/static/result.html"
            }

        };

        public async Task<StepActionResult> Publish(Guid id, SyncPublisherAction action, ActionArguments args)
        {
            if (args?.Options == null) throw new ArgumentNullException(nameof(args));

            if (id == Guid.Empty)
                id = Guid.NewGuid();

            var serverConfig = staticSiteService.Initialize(id, action, args);

            var dependencies = staticSiteService.GetDependencies(args.Options.Items, args.Callbacks);
            staticSiteService.StepCompleted(CalculateStepName);
            MoveToNextStep(action, args);

            // generate the razor for all the pages 
            GenerateHtml(dependencies, id, args);
            staticSiteService.StepCompleted(CreatePageStepName);
            MoveToNextStep(action, args);

            // grab all the media that is referenced in all the pages
            GatherMedia(dependencies, id, args);
            staticSiteService.StepCompleted(GatherMediaStepName);
            MoveToNextStep(action, args);

            // get the system files (css/scripts/etc)
            GatherFiles(id, args, serverConfig);
            staticSiteService.StepCompleted(FilesStepName);
            MoveToNextStep(action, args);


            // put this somewhere 
            Publish(id, args);
            staticSiteService.StepCompleted(UploadStepName);

            staticSiteService.PushComplete();

            return await Task.FromResult(new StepActionResult(true, id, args.Options, Enumerable.Empty<uSyncAction>()));
        }

        private void GenerateHtml(IEnumerable<uSyncDependency> dependencies, Guid id, ActionArguments args)
        {
            var pages = dependencies.Where(x => x.Udi.EntityType == UdiEntityType.Document).ToList();
            if (pages != null && pages.Any())
            {
                var count = pages.Count;

                foreach (var item in pages.Select((Page, Index) => new { Page, Index }))
                {
                    staticSiteService.SaveHtml(id, item.Page.Udi, () => args.Callbacks?.Update?.Invoke($"Generating: {item.Page.Name} html", item.Index, count));
                }
            }
        }

        private void GatherMedia(IEnumerable<uSyncDependency> dependencies, Guid id, ActionArguments args)
        {
            var media = dependencies.Where(x => x.Udi.EntityType == UdiEntityType.Media).ToList();
            foreach(var item in media.Select((Media, Index) => new {Media, Index}))
            {
                args.Callbacks?.Update?.Invoke($"Saving: {item.Media.Name}", item.Index, media.Count);
                staticSiteService.SaveMedia(id, item.Media.Udi);
            }
        }

        private void GatherFiles(Guid id, ActionArguments args, XElement serverConfig)
        {
            if (args.Options.IncludeFileHash)
            {
                var copyFolders = GetFolders(serverConfig);
                var copyFiles = new Dictionary<string, string>();
                var customFiles = new Dictionary<string, Stream>();

                staticSiteService.SaveFilesAndFolders(id, copyFolders, copyFiles, customFiles);
            }
        }

        /// <summary>
        ///  Get the list of additional folders to copy. 
        /// </summary>
        private List<string> GetFolders(XElement serverConfig)
        {
            if (serverConfig != null)
            {
                return serverConfig.Element("Folders").ValueOrDefault("~/css,~/scripts").ToDelimitedList().ToList();
            }

            return new List<string> { "~/css,~/scripts" };
        }

        private void Publish(Guid id, ActionArguments args)
        {
            staticSiteService.Publish(id, args.Target, args.Callbacks?.Update);
        }
    }
}
