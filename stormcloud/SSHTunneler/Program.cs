using stormserver;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace SSHTunneler
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting SSH Tunneler...");
            PermissionGroup defaultGroup = new PermissionGroup("default", new HashSet<int>(new int[] { 22 }), new HashSet<string>(new string[]{"0.0.0.0", "127.0.0.1", "localhost" }), false);
            List<PermissionGroup> groups = new List<PermissionGroup>();
            groups.Add(new PermissionGroup("encodeous-secure20", new HashSet<int>()));
            StormServer server = new StormServer(defaultGroup, CancellationToken.None, groups);
            server.StartAsync(new IPEndPoint(IPAddress.Any, 2345));
        }
    }
}
