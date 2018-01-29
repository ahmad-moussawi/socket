using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LiteBus
{
    public class Bus
    {
        // Thread signal.
        private ManualResetEvent sendSignal = new ManualResetEvent(false);
        private ManualResetEvent acceptSignal = new ManualResetEvent(false);
        private ManualResetEvent connectSignal = new ManualResetEvent(false);
        private ManualResetEvent receiveSignal = new ManualResetEvent(false);

        // The End Of Message Sign
        private const string EOM = "<EOM>";

        // The End Of File Sign (disconnect sign)
        private const string EOF = "<EOF>";

        /// <summary>
        /// List of clients connected in case this bus is acting as a Server
        /// </summary>
        /// <returns></returns>
        private List<Socket> clientsSockets = new List<Socket>();

        /// <summary>
        /// The current socket in case this bus is acting as a Client
        /// </summary>
        private Socket socket;

        private bool isServer = false;

        public BusEvents Events = new BusEvents();

        public Bus()
        {
        }

        public Socket CreateSocket(IPAddress ipAddress)
        {
            return new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Listen(int port) => Listen(IPAddress.Any.ToString(), port);

        /// <summary>
        /// Listen for incoming connections
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public void Listen(string ip, int port)
        {
            // Data buffer for incoming data.
            var bytes = new Byte[1024];

            var ipAddress = IPAddress.Parse(ip);
            var localEndPoint = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            var listener = CreateSocket(ipAddress);

            // Listen for incoming connections
            listener.Bind(localEndPoint);
            listener.Listen(100);

            // Flag this bus as a Server
            this.isServer = true;

            Console.WriteLine("Server is listening on {0}", listener.LocalEndPoint);

            while (true)
            {
                // Set the event to nonsignaled state.
                acceptSignal.Reset();

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine("Waiting for a connection...");

                listener.BeginAccept((ar) =>
                {
                    Socket client = listener.EndAccept(ar);

                    Events.ClientConnected(client);

                    // Add it to the list of clients
                    clientsSockets.Add(client);

                    // Signal the main thread to continue.
                    acceptSignal.Set();

                    // Create the state object.
                    var state = new StateObject
                    {
                        CurrentSocket = client
                    };

                    client.BeginReceive(
                        state.Buffer, 0,
                        StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback),
                        state
                    );

                    // Wait until receiving the content
                    receiveSignal.WaitOne();

                }, listener);

                // Wait until a connection is made before continuing.
                acceptSignal.WaitOne();
            }

        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var state = (StateObject)ar.AsyncState;
            var remote = state.CurrentSocket;

            // Read data from the client socket.
            int bytesRead = remote.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.
                state.StringBuilder.Append(GetString(state.Buffer, 0, bytesRead));

                content = state.StringBuilder.ToString();

                // Here we want to intercept the data to check if we have a
                // complete message(s), if so trigger the OnMessageReceived.
                if (content.Contains(EOM))
                {
                    var messageExtraction = extractMessages(content);

                    foreach (var message in messageExtraction.Messages)
                    {
                        Events.OnMessageReceived(remote, message);
                    }

                    // In case we have some incomplete messages keep it
                    // for the next iteration, so we expect to be completed later
                    content = messageExtraction.Remaining;

                    state.StringBuilder.Clear();

                }

                if (content.Contains(EOF))
                {
                    content = "";
                    Events.OnDisconnect(remote);
                    remote.Shutdown(SocketShutdown.Both);
                    remote.Close();

                    // Release the Receive signal
                    receiveSignal.Set();
                }
                else
                {
                    // Not all data received. Get more.
                    remote.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
                }
            }
        }

        /// <summary>
        /// Connect to a remote host
        /// </summary>
        /// <param name="ip">IP for the remote host</param>
        /// <param name="port">Port for the remote host</param>
        /// <param name="onConnect">Callback for the OnConnect event</param>
        public void Connect(string ip, int port, Action<Socket> onConnect)
        {
            this.Events.OnConnect = onConnect;
            Connect(ip, port);
        }

        /// <summary>
        /// Connect to a remote host
        /// </summary>
        /// <param name="ip">IP for the remote host</param>
        /// <param name="port">Port for the remote host</param>
        /// <param name="onConnect"></param>
        public void Connect(string ip, int port)
        {
            // Todo: some checks to validate the ip/port
            var ipAddress = IPAddress.Parse(ip);
            var remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            this.socket = CreateSocket(ipAddress);

            // Flag this bus as a client
            this.isServer = false;

            // Connect to the remote endpoint.
            this.socket.BeginConnect(remoteEP, (ar) =>
            {
                // Retrieve the socket from the state object.
                // Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                this.socket.EndConnect(ar);

                // Signal that the connection has been made.
                connectSignal.Set();

                Events.OnConnect(this.socket);

                // Create the state object.
                var state = new StateObject
                {
                    CurrentSocket = this.socket
                };

                this.socket.BeginReceive(
                    state.Buffer, 0,
                    StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback),
                    state
                );

                receiveSignal.WaitOne();

            }, this.socket);

            connectSignal.WaitOne();
        }

        /// <summary>
        /// Disconnect all connections
        /// </summary>
        public void Disconnect()
        {
            // If this is acting as a Server then we should remove disconnect all
            // Clients and shutdown the Server
            if (this.isServer)
            {

                this.clientsSockets.ForEach(socket =>
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                });

                this.clientsSockets.Clear();

            }
            else
            {
                // If we land here than this is a client and we need
                // to shutdown this client only
                this.socket.Shutdown(SocketShutdown.Both);
                this.socket.Close();
            }
        }

        /// <summary>
        /// Send a message to the remote device
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="data"></param>
        public void SendTo(Socket remote, string data)
        {
            var byteData = GetBytes(data + EOM);

            remote.BeginSend(byteData, 0, byteData.Length, 0, (ar) =>
            {
                // Retrieve the socket from the state object.
                // Socket client = (Socket)ar.AsyncState;

                // Here the data is sent to the remote device
                int bytesSent = remote.EndSend(ar);

                // Release send signal
                sendSignal.Set();

            }, remote);

            sendSignal.WaitOne();
        }

        public void Send(string data)
        {
            if (this.isServer)
            {
                throw new InvalidOperationException(
                    "`Send(string data)` method cannot be used while using the bus as a server"
                );
            }

            if (this.socket is null)
            {
                throw new InvalidOperationException(
                    "Invalid socket found, please connect before sending to the server"
                );
            }

            SendTo(this.socket, data);
        }

        /// <summary>
        /// Broadcast a message to all connected clients
        /// </summary>
        /// <param name="data"></param>
        public void Broadcast(string data)
        {
            if (!this.isServer)
            {
                throw new InvalidOperationException(
                    "`Broadcast(string data)` method cannot be used while using the bus as a client"
                );
            }

            this.clientsSockets.ForEach(socket =>
            {
                SendTo(socket, data);
            });

        }

        private MessageExtraction extractMessages(string data)
        {
            var result = new MessageExtraction();

            if (!data.EndsWith(EOM))
            {
                var lastIndex = data.LastIndexOf(EOM);
                result.Remaining = data.Substring(lastIndex + EOM.Length);
                data = data.Substring(0, lastIndex);
            }

            result.Messages = data.Split(new[] { EOM }, StringSplitOptions.RemoveEmptyEntries).ToList();

            return result;

        }

        private string GetString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        private string GetString(byte[] bytes, int index, int count)
        {
            return Encoding.UTF8.GetString(bytes, index, count);
        }

        private byte[] GetBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

    }

}

