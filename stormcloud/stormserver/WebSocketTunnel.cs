using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace stormserver
{
    class WebSocketTunnel
    {
        private HttpListenerWebSocketContext acceptWebSocketAsync;
        private int p;

        public WebSocketTunnel(HttpListenerWebSocketContext acceptWebSocketAsync, int p)
        {
            this.acceptWebSocketAsync = acceptWebSocketAsync;
            this.p = p;
        }
    }
}
