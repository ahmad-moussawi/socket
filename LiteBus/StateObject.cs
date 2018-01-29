using System.Text;
using System.Net.Sockets;

namespace LiteBus
{
    public class StateObject
    {
        // Client socket.
        public Socket CurrentSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] Buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder StringBuilder = new StringBuilder();
    }

}