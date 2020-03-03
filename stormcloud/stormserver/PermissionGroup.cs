using System;
using System.Collections.Generic;
using System.Text;

namespace stormserver
{
    public class PermissionGroup
    {
        public PermissionGroup(string token, HashSet<int> filteredPorts, HashSet<string> filteredEndpoints = null, bool blacklistMode = true)
        {
            authtoken = token;
            portFilter = filteredPorts;
            if(filteredEndpoints == null)
            {
                proxyEndpoints = new HashSet<string>();
            }
            else
            {
                proxyEndpoints = filteredEndpoints;
            }
            blacklist = blacklistMode;
        }
        public string authtoken;
        public HashSet<int> portFilter;
        public HashSet<string> proxyEndpoints;
        public bool blacklist;
    }
}
