using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Net.WebSockets;
using stormutils;
using System.Threading;

namespace stormclient
{
    public class ClientTunnel
    {
        Socket lSock;
        WebSocket rmSock;
        CancellationToken token;
        public ClientTunnel(Socket localSocket, WebSocket remoteSocket, CancellationToken stopToken)
        {
            lSock = localSocket;
            rmSock = remoteSocket;
            token = stopToken;
            new Thread(DataThread).Start();
        }

        private void DataThread()
        {
            try
            {
                if (rmSock.State == WebSocketState.Connecting)
                {
                    while (rmSock.State == WebSocketState.Connecting && !token.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }
                    if (rmSock.State != WebSocketState.Open)
                    {
                        Console.WriteLine("Failed to connect.");
                        return;
                    }
                }
                Console.WriteLine($"+ Tunneling Connection from {lSock.RemoteEndPoint} - > remote.");
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
                Console.WriteLine($"- Closing connection from {lSock.RemoteEndPoint} - > remote.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when reading data: " + e.Message);
            }
            try
            {
                lSock.Close();
                if (rmSock.State == WebSocketState.Open)
                {
                    rmSock.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                }
            }
            catch
            {

            }
        }
        void DataInbound(FlagClass<bool> connected)
        {
            while (rmSock.State == WebSocketState.Open && lSock.Connected && !token.IsCancellationRequested && connected.flag && SocketConnected())
            {
                try
                {
                    ArraySegment<byte> seg = new ArraySegment<byte>(new byte[1024]);
                    WebSocketReceiveResult res = rmSock.ReceiveAsync(seg, token).GetAwaiter().GetResult();
                    if (res.Count == 0) continue;
                    lSock.Send(seg.Slice(0, res.Count));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + e.StackTrace);
                    connected.flag = false;
                }
            }
        }

        void DataOutbound(FlagClass<bool> connected)
        {
            while (rmSock.State == WebSocketState.Open && lSock.Connected && !token.IsCancellationRequested && connected.flag && SocketConnected())
            {
                try
                {
                    ArraySegment<byte> seg = new ArraySegment<byte>(new byte[1024]);
                    int len = lSock.Receive(seg);
                    if (len == 0) continue;
                    rmSock.SendAsync(seg.Slice(0, len), WebSocketMessageType.Binary, true, token).Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + e.StackTrace);
                    connected.flag = false;
                }
            }
        }
        int cnt = 0;
        bool SocketConnected()
        {
            cnt++;
            if(cnt >= 10)
            {
                cnt = 0;
                bool part1 = lSock.Poll(1000, SelectMode.SelectRead);
                bool part2 = (lSock.Available == 0);
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
