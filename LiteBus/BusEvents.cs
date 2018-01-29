using System;
using System.Net.Sockets;

namespace LiteBus
{
    public class BusEvents
    {
        public Action<Socket, string> OnMessageReceived = (socket, message) => { };
        public Action<Socket> OnConnect = (socket) => { };
        public Action<Socket> OnDisconnect = (Socket) => { };
        public Action<Socket> ClientConnected = (Socket) => { };
    }

}
