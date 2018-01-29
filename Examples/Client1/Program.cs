using System;
using LiteBus;

namespace Client1
{
    class Program
    {
        static void Main(string[] args)
        {
            var bus = new Bus();

            bus.Events.OnMessageReceived = (socket, message) =>
            {
                Console.WriteLine("The server says: {0}", message);
            };

            bus.Connect("127.0.0.1", 11000, (socket) =>
            {
                Console.WriteLine(
                    "Connected on {0} to {1}",
                    socket.LocalEndPoint,
                    socket.RemoteEndPoint
                );

                bus.Send("Hello Server");
                bus.Send("I am the client 1");

            });

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
