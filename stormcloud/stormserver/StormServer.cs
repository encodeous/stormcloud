using stormutils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace stormserver
{
    public class StormServer
    {
        HttpListener listener;
        HashSet<int> allowedPorts;
        bool stopFlag = false;
        public StormServer(string[] url, HashSet<int> tunnelPorts) 
        {
            allowedPorts = tunnelPorts;
            listener = new HttpListener();
            foreach (var a in url)
            {
                listener.Prefixes.Add(a);
            }
            listener.Start();
            new Thread(Listener).Start();
        }

        private void Listener()
        {
            while (!stopFlag)
            {
                var c = listener.GetContext();
                string agent = c.Request.Headers.Get("agent");
                if(agent != null && agent == StormUtils.GetValidClientID())
                {
                    // accept client
                    string port = c.Request.Headers.Get("agent-port");
                    try
                    {
                        int p = int.Parse(port);
                        if (allowedPorts.Contains(p))
                        {
                            new Thread(() =>
                            {
                                new WebSocketTunnel(c.AcceptWebSocketAsync(null, TimeSpan.FromMilliseconds(10000)).Result, p);
                            }).Start();
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            listener.Stop();
        }

    }
}
