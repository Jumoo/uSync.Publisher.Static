using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core;
using uSync8.BackOffice.SyncHandlers;

namespace uSync.Publisher.Static
{
    public interface ISyncStaticDeployer
    {
        string Name { get; }
        string Alias { get; }

        Attempt<int> Deploy(string folder, XElement config, SyncUpdateCallback update);
    }
    
}
