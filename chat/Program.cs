using System.Net;
using System.Net.Sockets;
using System.Text;

namespace chat
{
    internal class Program
    {
        static void Main(string[] args)
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
                Socket.Select(readList, null, null, 100_000);

                foreach (var socket in readList)
                {
                    if (socket == listener)
                    {
                        var client = listener.Accept();
                        connections.Add(client);
                        Console.WriteLine("CONNECTED");
                    }
                    else
                    {
                        // At least 400 bytes to safely hold 100 UTF-8 characters.
                        var buffer = new byte[1024];
                        var received = socket.Receive(buffer);

                        if (received == 0)
                        {
                            connections.Remove(socket);
                            socket.Close();
                        }
                        else
                        {
                            var sender = (IPEndPoint)socket.RemoteEndPoint;

                            var msg = Encoding.UTF8.GetString(buffer, 0, received);
                            Console.WriteLine($"Message received from {sender.Address}");
                            Console.WriteLine($"Sender's Port: {sender.Port}");
                            Console.WriteLine($"Message: \"{msg}\"");
                        }
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
                            Console.WriteLine("{0,4}{1,-19}{2,8}", "ID: ", "IP Address", "Port No.");

                            for (int i =  0; i < connections.Count; i++)
                            {
                                Console.WriteLine("{0,4}{1,-19}{2,8}", i + 1 + ": ", connections[i].RemoteEndPoint, ((IPEndPoint)connections[i].RemoteEndPoint).Port);
                            }
                            break;
                        case "send":
                            // Take whitespace as part of message, instead of as delimiter.
                            var msgStr = string.Join(" ", argValues.Skip(2));

                            if (msgStr.Length > 100)
                            {
                                Console.WriteLine("Message can only be up to 100 characters long, including whitespace.");
                                continue;
                            }

                            if (int.TryParse(argValues[1], out var id))
                            {
                                // Offset by one b/c ID autoincrements starting from 1.
                                var sock = connections[id - 1];
                                
                                var msgBytes = Encoding.UTF8.GetBytes(msgStr);
                                _ = sock.Send(msgBytes);
                            }
                            break;
                    }

                    Thread.Sleep(20);
                }
            }
        }
    }
}
