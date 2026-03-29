using System.Net;
using System.Net.Sockets;
using System.Text;

namespace chat;

internal class Program
{
    static void Main(string[] args)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        if (args.Length == 0 || !int.TryParse(args[0], out var port))
        {
            Console.WriteLine("Usage: chat <port>");
            return;
        }

        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen();

        var connections = new List<Socket>();

        while (true)
        {
            var readList = new List<Socket>() { listener };

            readList.AddRange(connections);
            // Much like C's select method, but only for Socket types.
            Socket.Select(readList, null, null, 100_000);

            foreach (var socket in readList)
            {
                HandleSocketActivity(socket, listener, connections);
            }

            if (Console.KeyAvailable)
            {
                string input = Console.ReadLine() ?? string.Empty;

                // Adapted from slide 42, Chapter 2 (tokenizing command input).
                string[] argValues = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                string command = argValues[0];

                if (command == "exit")
                {
                    for (int i = connections.Count - 1; i >= 0; i--)
                    {
                        connections[i].Close();
                        connections.RemoveAt(i);
                    }
                    break;
                }

                switch (command.ToLower())
                {
                    case "help": DisplayHelp(); break;
                    case "myip": DisplayMyIp(); break;
                    case "myport": DisplayMyPort(listener); break;
                    case "connect": Connect(listener, connections, argValues); break;
                    case "list": ListConnections(connections); break;
                    case "terminate": TerminateConnection(connections, argValues); break;
                    case "send": SendMessage(connections, argValues); break;
                    default:
                        Console.WriteLine("\n" + "Unrecognized command. Use `help` to see list of commands." + "\n");
                        break;
                }

                Thread.Sleep(20);
            }
        }
    }

    internal static void HandleSocketActivity(Socket socket, Socket listener, List<Socket> connections)
    {
        if (socket == listener)
        {
            var client = listener.Accept();
            connections.Add(client);
        }
        else
        {
            // At least 400 bytes to safely hold 100 UTF-8 characters.
            var buffer = new byte[1024];
            var received = socket.Receive(buffer);

            if (received == 0) // Socket closure.
            {
                connections.Remove(socket);
                var remote = socket.RemoteEndPoint as IPEndPoint;
                Console.WriteLine("\n" + $"Connection closed with IP {remote!.Address} and Port No. {remote.Port}" + "\n");
                socket.Close();
            }
            else
            {
                var msg = Encoding.UTF8.GetString(buffer, 0, received);
                var sender = socket.RemoteEndPoint as IPEndPoint;

                Console.WriteLine("\n" + $"Message received from {sender!.Address}");
                Console.WriteLine($"Sender's Port: {sender.Port}");
                Console.WriteLine($"Message: \"{msg}\"" + "\n");
            }
        }
    }

    internal static void DisplayHelp()
    {
        Console.Write("\n");
        Console.WriteLine("myip         Display the IP address of the current process.");
        Console.WriteLine("myport       Display the port on which this process is listening for incoming connections.");
        Console.WriteLine("connect      Establish a new TCP connection to the specified <destination> IP at the specified <port no>.");
        Console.WriteLine("list         Display a numbered list of all the connections this process is part of.");
        Console.WriteLine("terminate    Terminate the connection by <id> as displayed by the list command.");
        Console.WriteLine("send         Send, to host by connection <id> as displayed by the list command, a <message>.");
        Console.WriteLine("exit         Close all connections and terminate this process.");
        Console.Write("\n");
    }

    internal static void DisplayMyIp()
    {
        string hostName = Dns.GetHostName();
        IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
        // Get the IPv4 address, specifically. Otherwise, gets the IPv6 address by default.
        // Snippet adapted from https://stackoverflow.com/a/36141575
        IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)!;
        Console.WriteLine("\n" + ipAddress + "\n");
    }

    internal static void DisplayMyPort(Socket listener)
    {
        var endpoint = listener.LocalEndPoint as IPEndPoint;
        Console.WriteLine("\n" + endpoint!.Port + "\n");
    }

    internal static void Connect(Socket listener, List<Socket> connections, string[] argValues)
    {
        if (argValues.Length < 3)
        {
            Console.WriteLine("\n" + "Usage: connect <ip> <port>" + "\n");
            return;
        }

        if (!IPAddress.TryParse(argValues[1], out var ip))
        {
            Console.WriteLine("\n" + "Invalid IP address." + "\n");
            return;
        }

        if (!int.TryParse(argValues[2], out int port) || (port is < 0 or > 65535))
        {
            Console.WriteLine("\n" + "Invalid port no." + "\n");
            return;
        }

        var endpoint = listener.LocalEndPoint as IPEndPoint;

        if (port == endpoint!.Port) // Check for self-connections.
        {
            Console.WriteLine("\n" + "Self-connections not allowed." + "\n");
            return;
        }

        // Check for attempt to create a connection with duplicate client IP and port.
        if (connections.Any(c => (c.RemoteEndPoint as IPEndPoint)!.Port == port && (c.RemoteEndPoint as IPEndPoint)!.Address == ip))
        {
            Console.WriteLine("\n" + "Duplicate connections not allowed." + "\n");
            return;
        }

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(ip, port));
            connections.Add(socket);
        }

    internal static void ListConnections(List<Socket> connections)
    {
        Console.WriteLine("\n" + "{0,4}{1,-19}{2,8}", "ID: ", "IP Address", "Port No.");

        for (int i = 0; i < connections.Count; i++)
        {
            Console.WriteLine(
                "{0,4}{1,-19}{2,8}",
                i + 1 + ": ",
                connections[i].RemoteEndPoint,
                (connections[i].RemoteEndPoint as IPEndPoint)!.Port);
        }

        Console.Write("\n");
    }

    internal static void TerminateConnection(List<Socket> connections, string[] argValues)
    {
        if (int.TryParse(argValues[1], out var terminateId))
        {
            if (terminateId > connections.Count)
            {
                Console.WriteLine("\n" + $"No valid connection with ID: {terminateId}" + "\n");
            }
            else
            {
                connections[terminateId - 1].Close();
                connections.RemoveAt(terminateId - 1);
            }
        }
        else
        {
            Console.WriteLine("\n" + "Usage: terminate <connection id>" + "\n");
        }
    }

    internal static void SendMessage(List<Socket> connections, string[] argValues)
    {
        // Take whitespace as part of message, instead of as delimiter.
        var msgStr = string.Join(" ", argValues.Skip(2));

        if (msgStr.Length > 100)
        {
            Console.WriteLine("\n" + "Message can only be 100 characters long, including whitespace." + "\n");
            return;
        }

        if (int.TryParse(argValues[1], out var id))
        {
            // Offset by one b/c ID autoincrements starting from 1.
            var sock = connections[id - 1];

            var msgBytes = Encoding.UTF8.GetBytes(msgStr);
            _ = sock.Send(msgBytes);
        }
    }
}
