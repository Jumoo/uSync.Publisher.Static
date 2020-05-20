using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core;
using uSync.Expansions.Core.Physical;
using uSync8.BackOffice.SyncHandlers;
using uSync8.Core.Extensions;

namespace uSync.Publisher.Static.Deployers
{
    public class StaticFolderDeployer : ISyncStaticDeployer
    {
        private readonly TemplateFileService templateFileService;

        public StaticFolderDeployer(TemplateFileService templateFileService)
        {
            this.templateFileService = templateFileService;
        }

        public string Name => "Static Folder Deployer";

        public string Alias => "folder";

        public Attempt<int> Deploy(string folder, XElement config, SyncUpdateCallback update)
        {
            var settings = LoadSettings(config);

            update?.Invoke($"Copying site to {Path.GetFileName(folder)}", 1, 2);

            Directory.CreateDirectory(settings.Folder);
            templateFileService.CopyFolder(folder, settings.Folder);

            return Attempt.Succeed(1);
        }

        private FolderConfig LoadSettings(XElement config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var settings = new FolderConfig();
            settings.Folder = config.Element("folder").ValueOrDefault(string.Empty);
            if (string.IsNullOrWhiteSpace(settings.Folder)) throw new ArgumentOutOfRangeException(nameof(config), "No Folder setting");

            return settings;
        }

        private class FolderConfig
        {
            public string Folder { get; set; }
        }
    }
}
