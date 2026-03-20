using System.Net;
using System.Net.Sockets;

namespace chat
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // TODO: throw exception if listening port number not provided as command line argument.
            var address = int.Parse(args.First());
            socket.Bind(new IPEndPoint(IPAddress.Any, address));

            while (true)
            {
                string? input = Console.ReadLine();

                if (input == "exit") // #8
                {
                    break;
                }

                switch (input)
                {
                    case "myip": // #2
                        string hostName = Dns.GetHostName();
                        IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
                        // Get the IPv4 address, specifically. Otherwise, gets the IPv6 address by default.
                        // Snippet adapted from https://stackoverflow.com/a/36141575
                        IPAddress ipAddress = ipHostInfo.AddressList
                            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                        Console.WriteLine(ipAddress);
                        break;
                }
            }
        }
    }
}
