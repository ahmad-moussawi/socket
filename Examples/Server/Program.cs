using System;
using System.Threading;
using LiteBus;

namespace ServerClientMultipleThreads
{
    public class Program
    {
        public static int Main(String[] args)
        {

            var server = new Bus();

            server.Events.OnMessageReceived = (client, message) =>
            {

                Console.WriteLine("Message received from {0}: {1}", client.RemoteEndPoint, message);

                server.SendTo(client, $"Reply: {message}");

            };

            server.Events.ClientConnected = client =>
            {

                Console.WriteLine("Client {0} connected", client.RemoteEndPoint);

                server.SendTo(client, "hello client");

            };

            server.Events.OnDisconnect = client =>
            {
                Console.WriteLine("Client {0} disconnected", client.RemoteEndPoint);
            };

            server.Listen(11000);


            Console.WriteLine("Server Started");
            Console.Read();

            return 0;
        }

    }
}

