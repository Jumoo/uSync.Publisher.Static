using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public string Name => "Static Site Publisher";
        public string Alias => "static";

        public string ConfigView => uSyncPublisher.PluginFolder + "publishers/static/serverConfig.html";

        private readonly uSyncOutgoingService outgoingService;
        private readonly uSyncStaticSiteService staticSiteService;

        public SyncStaticSitePublisher(uSyncPublisherConfig config,
            IProfilingLogger logger,
            IGlobalSettings settings,
            uSyncOutgoingService outgoingService,
            uSyncIncomingService incomingService,            
            uSyncStaticSiteService staticSiteService)
            : base(config, logger, settings, incomingService)
        {
            this.outgoingService = outgoingService;
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
                    new SyncPublisherStep("Calculate", "icon-science", "Calculating"),
                    new SyncPublisherStep("Creating Pages", "icon-box", "Files"),
                    new SyncPublisherStep("Gathering Media", "icon-picture", "Media"),
                    new SyncPublisherStep("Files", "icon-documents", "Files"),
                    new SyncPublisherStep("Upload", "icon-truck usync-truck", "Uploading")
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

            

            var dependencies = outgoingService.GetItemDependencies(args.Options.Items, args.Callbacks);
            MoveToNextStep(action, args);

            // generate the razor for all the pages 
            GenerateHtml(dependencies, id, args);
            MoveToNextStep(action, args);

            // grab all the media that is referenced in all the pages
            GatherMedia(dependencies, id, args);
            MoveToNextStep(action, args);

            // get the system files (css/scripts/etc)
            GatherFiles(dependencies, id, args);
            MoveToNextStep(action, args);


            // put this somewhere 
            Publish(id, args);

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
                    var pageId = staticSiteService.GetItemId(item.Page.Udi);
                    if (pageId > 0)
                    {
                        args.Callbacks?.Update?.Invoke($"Generating: {item.Page.Name} html", item.Index, count);

                        staticSiteService.SaveHtml(id, pageId);
                        // var html = staticSiteService.GenerateItemHtml(pageId);
                        // var path = staticSiteService.GetItemPath(pageId);

                        // save the html at folder / path. 
                    }
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

        private void GatherFiles(IEnumerable<uSyncDependency> dependencies, Guid id, ActionArguments args)
        {
            if (args.Options.IncludeFileHash)
            {
                staticSiteService.SaveFolders(id, GetFolders(args.Target));
            }
        }

        /// <summary>
        ///  Get the list of additional folders to copy. 
        /// </summary>
        private string[] GetFolders(string serverAlias)
        {
            var serverConfig = staticSiteService.LoadServerConfig(serverAlias);
            if (serverConfig != null)
            {
                return serverConfig.Element("Folders").ValueOrDefault("~/css,~/scripts").ToDelimitedList().ToArray();
            }

            return new string[] { "~/css,~/scripts" };
        }


        private void Publish(Guid id, ActionArguments args)
        {
            staticSiteService.Publish(id, args.Target, args.Callbacks?.Update);
        }
    }
}
