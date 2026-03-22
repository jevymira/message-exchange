using System.Net;
using System.Net.Sockets;

namespace chat
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // TODO: throw exception if listening port number not provided as command line argument.
            var address = int.Parse(args.First());
            var ipEndPoint = new IPEndPoint(IPAddress.Any, address);
            listener.Bind(ipEndPoint);
            listener.Listen();

            var connections = new List<Socket>();

            while (true)
            {
                var readList = new List<Socket>() { listener };
                /*
                var writeList = new List<Socket>();
                var errorList = new List<Socket>();
                */

                readList.AddRange(connections);
                // Much like C's select method, but only for Socket types.
                Socket.Select(readList, null, null, 1_000_000);

                foreach (var socket in readList)
                {
                    if (socket == listener)
                    {
                        var client = listener.Accept();
                        connections.Add(client);
                        Console.WriteLine("CONNECTED");
                    }
                }

                if (Console.KeyAvailable)
                {
                    string? input = Console.ReadLine();

                    // Adapted from slide 42, Chapter 2 (tokenizing command input).
                    string[] argValues = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    int argCount = argValues.Length;

                    if (argValues[0] == "exit") // #8
                    {
                        break;
                    }

                    switch (argValues[0])
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
                        case "myport":
                            var listeningIpEndPoint = (IPEndPoint)listener.LocalEndPoint;
                            Console.WriteLine(listeningIpEndPoint.Port);
                            break;
                        case "connect":
                            if (argValues.Length < 3)
                            {
                                Console.WriteLine("Usage: connect <ip> <port>");
                                continue;
                            }

                            string ipStr = argValues[1];
                            string portStr = argValues[2];

                            if (IPAddress.TryParse(ipStr, out var ip) && int.TryParse(portStr, out int port))
                            {
                                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                sock.Connect(new IPEndPoint(ip, port));
                                connections.Add(sock);
                            }
                            else
                            {
                                Console.WriteLine("Usage: connect <ip> <port>");
                            }
                            break;
                        case "list":
                            foreach (var connection in connections)
                            {
                                Console.WriteLine(connection.RemoteEndPoint);
                            }
                            break;
                    }

                    Thread.Sleep(20);
                }
            }
        }
    }
}
