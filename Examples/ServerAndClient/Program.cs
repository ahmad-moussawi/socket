using System;
using System.Threading;
using LiteBus;

namespace ServerClientMultipleThreads
{
    public class Program
    {
        public static int Main(String[] args)
        {
            new Thread(() =>
            {
                var server = new Bus();

                server.Events.OnMessageReceived = (client, message) =>
                {
                    Console.WriteLine("Message received from {0}: {1}", client.RemoteEndPoint, message);

                    server.SendTo(client, "Ok Received: " + message);
                };

                server.Events.ClientConnected = client =>
                {
                    Console.WriteLine("Client {0} connected", client.RemoteEndPoint);

                    // Thread.Sleep(100);
                    server.SendTo(client, "hello client");
                };

                server.Events.OnDisconnect = client =>
                {
                    Console.WriteLine("Client {0} disconnected", client.RemoteEndPoint);
                };

                server.Listen(11000);
            }).Start();


            new Thread(() =>
            {
                var client = new Bus();

                client.Events.OnMessageReceived = (socket, message) =>
                {
                    Console.WriteLine("Client received a message: " + message);
                };

                Thread.Sleep(3000);

                client.Connect("127.0.0.1", 11000, (socket) =>
                {
                    Console.WriteLine("Client {0} Connected to {1}", socket.LocalEndPoint, socket.RemoteEndPoint);

                    client.Send("Thanks for accepting me :D");
                    client.Send("Message one");

                    Thread.Sleep(4000);
                    client.Send("Message two after four seconds");

                });


            }).Start();

            Console.WriteLine("Started");
            Console.Read();

            return 0;
        }

    }
}

