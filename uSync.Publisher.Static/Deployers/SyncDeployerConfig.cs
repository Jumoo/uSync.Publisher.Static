using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace uSync.Publisher.Static
{
    /// <summary>
    ///  Config for a deployer.
    /// </summary>
    /// <remarks>
    ///  Not all the settings might be needed, but hopefully these cover the main ones.
    /// </remarks>
    [JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
    public class SyncDeployerConfig
    {
        public string Server { get; set; }
        public string Location { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string Extra { get; set; }
    }
}
