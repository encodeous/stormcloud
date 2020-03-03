using stormutils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stormclient
{
    public class StormClient
    {
        public delegate void DisconnectDelegate();
        public event DisconnectDelegate DisconnectionEvent;
        private Uri connectServer;
        private string authToken;
        private string remoteHostname;
        private int remotePort;
        private Socket localSock;
        public StormClient(Uri server, string token, string hostname, int port)
        {
            connectServer = server;
            authToken = token;
            remoteHostname = hostname;
            remotePort = port;
        }
        public StormClient(Uri server, string hostname, int port)
        {
            connectServer = server;
            authToken = "default";
            remoteHostname = hostname;
            remotePort = port;
        }
        private async Task<WebSocket> Connect(CancellationToken stopToken, ProtocolType protocolType)
        {
            string agent = StormUtils.GetValidClientID();
            ClientWebSocket socket = new ClientWebSocket();
            socket.Options.SetRequestHeader(agent, agent);
            socket.Options.SetRequestHeader(agent + "-token", authToken);
            socket.Options.SetRequestHeader(agent + "-protocol", ((int)protocolType).ToString());
            socket.Options.SetRequestHeader(agent + "-port", remotePort.ToString());
            socket.Options.SetRequestHeader(agent + "-target", remoteHostname);
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await socket.ConnectAsync(connectServer, stopToken);
            return socket;
        }
        public void OpenGateway(IPEndPoint localEndpoint, ProtocolType protocolType, CancellationToken stopToken)
        {
            localSock = new Socket(localEndpoint.AddressFamily, SocketType.Stream, protocolType);
            localSock.NoDelay = true;
            localSock.Bind(localEndpoint);
            localSock.Listen(10);
            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    var tClient = localSock.Accept();
                    tClient.NoDelay = true;
                    WebSocket socket = Connect(stopToken, protocolType).Result;
                    new ClientTunnel(tClient, socket, stopToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in Gateway" + e.Message);
                }
            }
            localSock.Shutdown(SocketShutdown.Both);
            localSock.Close();
        }

    }
}
