using stormclient;
using stormserver;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace stormcloud
{
    class Program
    {
        static StormClient client;
        static CancellationTokenSource src;
        static void Main(string[] args)
        {
            Console.WriteLine("yes/no for stormserver");
            var k = Console.ReadLine();
            src = new CancellationTokenSource();
            if (k == "yes")
            {
                StormServer server = new StormServer(new PermissionGroup("lol", new HashSet<int>()), src.Token);
                server.StartAsync(new IPEndPoint(IPAddress.Any, 2222));
            }
            else
            {
                client = new StormClient(new Uri("ws://sc.encodeous.me"), "encodeous-secure20", "0.0.0.0", 22);
                client.OpenGateway(new IPEndPoint(IPAddress.Any,50), ProtocolType.Tcp, src.Token);
                client.DisconnectionEvent += Disconnection;
            }
            Console.ReadLine();
            src.Cancel();
        }
        public static void Disconnection()
        {
            Console.WriteLine("Disconnected! Reconnecting...");
        }
    }
}
