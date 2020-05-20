using System.Net;
using FluentFTP;
using System.IO;
using System.Security.Authentication;
using System;
using Renci.SshNet;
using Umbraco.Core;
using System.Linq;
using System.Xml.Linq;
using uSync8.Core.Extensions;
using uSync8.BackOffice.SyncHandlers;

namespace uSync.Publisher.Static
{
    public class SyncStaticSFtpDeployer : ISyncStaticDeployer
    {
        public string Name => "SFTP Deployer";
        public string Alias => "sftp";

        public SyncStaticSFtpDeployer() { }

        public Attempt<int> Deploy(string folder, XElement configNode, SyncUpdateCallback callback)
        {
            var config = LoadSettings(configNode);
            
            try
            {
                var cleanFolder = $"{folder.Replace("/", "\\")}\\".Replace("\\\\", "\\");

                var connectionInfo = new ConnectionInfo(config.Server,
                                        config.Username,
                                        new PasswordAuthenticationMethod(config.Username, config.Password),
                                        new PrivateKeyAuthenticationMethod("rsa.key"));

                int count = 0;

                using (var client = new SftpClient(connectionInfo))
                {

                    callback?.Invoke("Connecting to Server", 1, 100);
                    client.Connect();
                    count = UploadFolder(config.Folder, cleanFolder, "", client, callback);

                    callback?.Invoke("Complete", 100, 100);
                    client.Disconnect();
                }

                return Attempt.Succeed(count);


            }
            catch (Exception ex)
            {
                return Attempt.Fail(0, ex);
            }
        }


        private int UploadFolder(string siteRoot, string localRoot, string folder, SftpClient client, SyncUpdateCallback callback)
        {
            if (!client.IsConnected)
                throw new FtpException("Not connected");

            var path = Path.Combine(localRoot, folder);

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException("Path not found: " + path);

            var dest = $"/{siteRoot}/{folder}";
            try
            {
                client.CreateDirectory(dest);
            }
            catch { }

            callback?.Invoke($"Syncing {folder}", 5, 10);

            var files = client.SynchronizeDirectories(path, dest, "*.*");
            var total = files.Count();

            foreach(var child in Directory.GetDirectories(path))
            {
                var childFolder = child.Substring(localRoot.Length)
                    .Replace("\\", "/");

                total += UploadFolder(siteRoot, localRoot, childFolder, client, callback);
            }

            return total;
        }

        private Credentials LoadSettings(XElement node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            var config = new Credentials();

            config.Server = node.Element("server").ValueOrDefault(string.Empty);
            config.Username = node.Element("username").ValueOrDefault(string.Empty);
            config.Password = node.Element("password").ValueOrDefault(string.Empty);
            config.Folder = node.Element("folder").ValueOrDefault(string.Empty);

            return config; 
        }

        private class Credentials
        {
            public string Server { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Folder { get; set; }
        }
    }
}
