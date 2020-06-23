using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using uSync.Publisher.Publishers;
using uSync8.Core.Dependency;

namespace uSync.Publisher.Static.Extensions
{
    /// <summary>
    /// A base class which can be extended to override only one or two methods from the extension interface, without worrying about the other methods
    /// </summary>
    public abstract class StaticPublisherExtension : IStaticPublisherExtension
    {
        public virtual object Initialize(Guid id, string syncRoot, SyncPublisherAction action, ActionArguments args, XElement serverConfig) => null;

        public virtual void PushComplete(object state) { }

        public virtual void StepCompleted(object state, string stepName) { }

        public virtual void TransformCustomFilesAndFolders(object state, ICollection<string> copyFolders, IDictionary<string, string> copyFiles, IDictionary<string, Stream> customFiles) { }

        public virtual void TransformDependecies(object state, ICollection<uSyncDependency> dependencies) { }

        public virtual void TransformHtml(object state, IPublishedContent content, ref string path, ref string html) { }

        public virtual void TransformMedia(object state, Udi mediaId, string destinationPath) { }
    }
}
