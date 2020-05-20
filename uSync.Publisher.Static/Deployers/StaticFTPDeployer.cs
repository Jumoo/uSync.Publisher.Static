using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentFTP;
using Umbraco.Core;
using uSync8.BackOffice.SyncHandlers;
using uSync8.Core.Extensions;

namespace uSync.Publisher.Static.Deployers
{
    public class StaticFTPDeployer : ISyncStaticDeployer
    {
        public string Name => "FTP Deployer";

        public string Alias => "ftp";

        public Attempt<int> Deploy(string folder, XElement config, SyncUpdateCallback update)
        {
            var settings = LoadSettings(config);

            var client = new FtpClient(settings.Server);
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            client.EncryptionMode = FtpEncryptionMode.Explicit;
            client.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
            client.ValidateCertificate += new FtpSslValidation(OnValidateCertificate);

            update?.Invoke("connecting to server", 1, 100);

            client.Connect();

            var count = UploadFolder(settings.Folder, folder, "", client, update);

            client.Disconnect();

            return Attempt.Succeed(count);
        }

        void OnValidateCertificate(FtpClient control, FtpSslValidationEventArgs e)
        {
            // add logic to test if certificate is valid here
            e.Accept = true;
        }

        private int UploadFolder(string serverRoot, string localRoot, string folder, FtpClient client, SyncUpdateCallback update)
        {
            var serverPath = $"{serverRoot}/{folder}".Replace("\\", "/");

            if (!client.DirectoryExists(serverPath))
                client.CreateDirectory(serverPath);

            var localPath = Path.Combine(localRoot, folder);

            var localDir = new DirectoryInfo(localPath);
            var files = localDir.GetFiles();

            // update?.Invoke($"Updating Folder {Path.GetFileName(folder)}", 5, 10);
            // var count = client.UploadFiles(files, serverPath);

            int count = 0;
            foreach (var file in files)
            {
                update?.Invoke($"Uploading {folder}/{file.Name}", 5, 10);
                client.UploadFile(file.FullName, serverPath + "/" + file.Name);
                count++;
            }

            foreach (var childFolder in localDir.GetDirectories())
            {
                var relativeFolder = childFolder.FullName.Substring(localRoot.Length+1);
                count += UploadFolder(serverRoot, localRoot, relativeFolder, client, update);
            }

            return count;

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
