using stormutils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace stormserver
{
    public class StormServer
    {
        WebSocketListener listener;
        PermissionGroup defaultGroup;
        Dictionary<string, PermissionGroup> authGroups = new Dictionary<string, PermissionGroup>();
        CancellationToken stopToken;
        public StormServer(PermissionGroup defaultPermissions, CancellationToken token, List<PermissionGroup> groups = null)
        {
            stopToken = token;
            defaultGroup = defaultPermissions;
            if (groups != null)
            {
                foreach (PermissionGroup g in groups)
                {
                    authGroups.Add(g.authtoken, g);
                }
            }
        }

        public async void StartAsync(IPEndPoint listenEndpoint)
        {
            var bufferSize = 1024 * 8; // 8KiB
            var bufferPoolSize = 100 * bufferSize; // 800KiB pool
            var options = new WebSocketListenerOptions
            {
                SubProtocols = new[] { "binary" },
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                PingMode = PingMode.Manual,
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize),
            };
            options.Standards.RegisterRfc6455(factory =>
            {
                factory.MessageExtensions.RegisterDeflateCompression();
            });
            options.Transports.ConfigureTcp(tcp =>
            {
                tcp.BacklogSize = 100; // max pending connections waiting to be accepted
                    tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });
            options.SupportedSslProtocols = SslProtocols.Tls12;
            listener = new WebSocketListener(listenEndpoint, options);
            listener.StartAsync().Wait();
            Console.WriteLine("Storm Server 1.1 started on " + listenEndpoint.Address + ":" + listenEndpoint.Port + ".");
            Console.WriteLine("Loaded " + authGroups.Count + " authgroup(s).");
            Listener();
        }

        public async void StartAsync(Uri[] listenEndpoint)
        {
            var bufferSize = 1024 * 4;
            var bufferPoolSize = 1000 * bufferSize;
            var options = new WebSocketListenerOptions
            {
                SubProtocols = new[] { "binary" },
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                PingMode = PingMode.LatencyControl,
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize),
            };
            options.Standards.RegisterRfc6455();
            options.Transports.ConfigureTcp(tcp =>
            {
                tcp.BacklogSize = 1000; // max pending connections waiting to be accepted
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });
            options.SupportedSslProtocols = SslProtocols.Tls12;
            listener = new WebSocketListener(listenEndpoint, options);
            listener.StartAsync().Wait();
            Console.WriteLine("Storm Server 1.1 started on " + string.Join(", ", Array.ConvertAll(listenEndpoint, e => e.ToString())) + ".");
            new Thread(() =>
            {
                Listener();
            }).Start();
        }

        void Listener()
        {
            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    WebSocket c = listener.AcceptWebSocketAsync(stopToken).Result;
                    if (c == null)
                    {
                        if (stopToken.IsCancellationRequested || !listener.IsStarted)
                            break;
                        continue;
                    }
                    if (!c.IsConnected || !HandleConnection(c))
                    {
                        c.HttpResponse.Status = HttpStatusCode.Forbidden;
                        if (c.IsConnected)
                        {
                            c.CloseAsync();
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error handling client. " + e.Message);
                }
            }
            listener.StopAsync();
        }
        private bool HandleConnection(WebSocket c)
        {
            try
            {
                string agent = c.HttpRequest.Headers.Get(StormUtils.GetValidClientID());
                if (agent != null && agent == StormUtils.GetValidClientID())
                {
                    // accept connection
                    string token = c.HttpRequest.Headers.Get(agent + "-token");
                    string port = c.HttpRequest.Headers.Get(agent + "-port");
                    string target = c.HttpRequest.Headers.Get(agent + "-target");
                    string protocol = c.HttpRequest.Headers.Get(agent + "-protocol");
                    if (PermissionCheck(token, port, target))
                    {
                        new Thread(() =>
                        {
                            Console.WriteLine($"Client has successfully authenticated! {c.HttpRequest.RemoteEndPoint} - > {target} on port {port}.");
                            var sock = new WebSocketTunnel(c, target, int.Parse(port), (ProtocolType)int.Parse(protocol), stopToken);
                            sock.Start();
                        }).Start();
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Blocked connection from {c.HttpRequest.RemoteEndPoint} - > {target}:{port}, failed security check.");
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Unhandled exception from handling connection! " + e.Message);
            }
            return false;
        }

        private bool PermissionCheck(string token, string port, string target)
        {
            if (token == null || port == null || target == null)
            {
                return false;
            }
            try
            {
                int iport = int.Parse(port);
                if (authGroups.ContainsKey(token))
                {
                    // group permissions
                    PermissionGroup group = authGroups[token];
                    if (group.blacklist)
                    {
                        if (group.portFilter.Contains(iport) || group.proxyEndpoints.Contains(target))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!group.portFilter.Contains(iport) || !group.proxyEndpoints.Contains(target))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // default permissions
                    if (defaultGroup.blacklist)
                    {
                        if (defaultGroup.portFilter.Contains(iport) || defaultGroup.proxyEndpoints.Contains(target))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!defaultGroup.portFilter.Contains(iport) || !defaultGroup.proxyEndpoints.Contains(target))
                        {
                            return false;
                        }
                    }
                }

            }
            catch (OverflowException e) { return false; }
            catch (FormatException e) { return false; }
            catch (ArgumentNullException e) { return false; }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception from handling connection! " + e.Message);
                return false;
            }
            return true;
        }
    }
}
