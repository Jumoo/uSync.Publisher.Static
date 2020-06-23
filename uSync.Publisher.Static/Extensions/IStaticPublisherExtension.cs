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
    public interface IStaticPublisherExtension
    {
        /// <summary>
        /// Initializes the extension, and returns a state object that is passed to other extension methods to maintain state between methods
        /// </summary>
        /// <param name="id">The unique ID of the push</param>
        /// <param name="syncRoot">The root folder which, when combined with the ID, forms the path where the static files are staged</param>
        /// <param name="action">The action being executed</param>
        /// <param name="args">The files and options specified in the config and by the user for this push</param>
        /// <param name="serverConfig">The configuration element for the server being deployed to</param>
        /// <returns>A state object which is passed to other extension methods</returns>
        object Initialize(Guid id, string syncRoot, SyncPublisherAction action, ActionArguments args, XElement serverConfig);

        /// <summary>
        /// Allows the extension to modify the HTML of a generated page, perhaps to update site-specific content on the page
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        /// <param name="content">The item whose HTML was generated</param>
        /// <param name="path">The path the item will be saved to</param>
        /// <param name="html">The generated HTML content</param>
        void TransformHtml(object state, IPublishedContent content, ref string path, ref string html);

        /// <summary>
        /// Allows for modifying a media item after it has been copied to the staging area
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        /// <param name="mediaId">The media item that was copied</param>
        /// <param name="destinationPath">The path in the staging area where the media item can be found</param>
        void TransformMedia(object state, Udi mediaId, string destinationPath);

        /// <summary>
        /// Allows for adding or modifying the folder and files that will be copied to the staging area, as well as for adding custom files that will be added to the staging area
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        /// <param name="copyFolders">The folders which will be copied into the staging area</param>
        /// <param name="copyFiles">The files which will be copied into the staging area, with the key being the source file name, and the value being the destination file name</param>
        /// <param name="customFiles">Dynamically generated files that will be added to the staging area</param>
        void TransformCustomFilesAndFolders(object state, ICollection<string> copyFolders, IDictionary<string, string> copyFiles, IDictionary<string, Stream> customFiles);

        /// <summary>
        /// Allows for altering the set of dependencies that will be updated as part of this push
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        /// <param name="dependencies">The dependencies which will be updated</param>
        void TransformDependecies(object state, ICollection<uSyncDependency> dependencies);

        /// <summary>
        /// Notifies an extension that a step has completed
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        /// <param name="stepName">One of the StepName constants on the SyncStaticSitePublisher class</param>
        void StepCompleted(object state, string stepName);

        /// <summary>
        /// Notifies an extension that all processing has completed
        /// </summary>
        /// <param name="state">The state returned from the Initialize method</param>
        void PushComplete(object state);
    }
}
