using stormutils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace stormserver
{
    class WebSocketTunnel
    {
        private WebSocket socket;
        private Socket remoteSock;
        private CancellationToken stopToken;
        bool started;
        string remoteHostname;
        int remotePort;
        ProtocolType protocol;
        public WebSocketTunnel(WebSocket sock, string hostName, int port, ProtocolType endPointProtocol, CancellationToken cancellationToken)
        {
            remoteHostname = hostName;
            remotePort = port;
            socket = sock;
            protocol = endPointProtocol;
            stopToken = cancellationToken;
        }

        public void Start()
        {
            if (!started)
            {
                started = true;
            }
            else
            {
                throw new Exception("Tunnel Already Started");
            }
            try
            {
                Console.WriteLine($"+ Tunnel. {socket.RemoteEndpoint} - > {remoteHostname}:{remotePort} opened.");
                remoteSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, protocol);
                remoteSock.NoDelay = true;
                remoteSock.ReceiveTimeout = 5000;
                remoteSock.Connect(remoteHostname,remotePort);
                FlagClass<bool> connected = new FlagClass<bool>(true);
                new Thread(() => {
                    DataInbound(connected);
                }).Start();
                new Thread(() => {
                    DataOutbound(connected);
                }).Start();
                while (connected.flag)
                {
                    Thread.Sleep(100);
                }
                Console.WriteLine($"- Tunnel. {socket.RemoteEndpoint} - > {remoteHostname}:{remotePort} closed.");
                remoteSock.Shutdown(SocketShutdown.Both);
                remoteSock.Close();
                socket.CloseAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception from handling connection! " + e.Message);
            }
        }

        void DataInbound(FlagClass<bool> connected)
        {

            while (connected.flag && socket.IsConnected && remoteSock.Connected && !stopToken.IsCancellationRequested && SocketConnected())
            {
                try
                {
                    using (WebSocketMessageReadStream str = socket.ReadMessageAsync(stopToken).GetAwaiter().GetResult())
                    {
                        if (str == null)
                        {
                            connected.flag = false;
                            return;
                        }
                        ArraySegment<byte> seg = new ArraySegment<byte>(new byte[1024]);
                        int cnt = str.Read(seg);
                        if (cnt == 0) continue;
                        remoteSock.Send(seg.Slice(0, cnt));
                        str.CloseAsync();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + e.StackTrace);
                    connected.flag = false;
                }

            }
            connected.flag = false;
        }

        void DataOutbound(FlagClass<bool> connected)
        {
            using (WebSocketMessageWriteStream str = socket.CreateMessageWriter(WebSocketMessageType.Binary))
            {
                while (connected.flag && socket.IsConnected && remoteSock.Connected && !stopToken.IsCancellationRequested && SocketConnected())
                {
                    try
                    {
                        ArraySegment<byte> seg = new ArraySegment<byte>(new byte[1024]);
                        int cnt = remoteSock.Receive(seg);
                        if (cnt == 0) continue;
                        str.Write(seg.Slice(0, cnt));
                        str.FlushAsync().Wait();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + e.StackTrace);
                        connected.flag = false;
                    }
                }
            }
            connected.flag = false;
        }
        int count = 0;
        bool SocketConnected()
        {
            count++;
            if (count >= 10)
            {
                count = 0;
                bool part1 = remoteSock.Poll(1000, SelectMode.SelectRead);
                bool part2 = (remoteSock.Available == 0);
                if (part1 && part2)
                    return false;
                else
                    return true;
            }
            else
            {
                return true;
            }
        }
    }
}
