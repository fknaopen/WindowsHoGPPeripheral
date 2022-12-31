using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;


namespace bleHIDsockClient
{
    class Program
    {
        static void Main(string[] args)
        {
            SendUDPdata(0xe9);  Thread.Sleep(2000);
            SendUDPdata(0xcd);  Thread.Sleep(2000);
            SendUDPdata(0xea);  Thread.Sleep(2000);
            SendUDPdata(0x00);
        }
        static async void SendUDPdata(byte keycode)
        {
            var remote = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 55740);

            var client = new UdpClient();
            client.Connect(remote);

            byte[] buffer = { keycode };
            Console.Write(string.Format("{0:x2} ", buffer[0]));
            await client.SendAsync(buffer, 1);
            client.Close();
        }
    }
}
