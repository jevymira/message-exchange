using System.Net;
using System.Net.Sockets;
using System.Text;

namespace chat;

internal class Program
{
    static void Main(string[] args)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Validate `port` input on initial `./chat <port>` startup call.
        if (args.Length == 0 || !int.TryParse(args[0], out var port) || (port is < 0 or > 65535))
        {
            Console.WriteLine("\n" + "Usage: chat <port>" + "\n");
            return;
        }

        // Bind listener socket to supplied <port>.
        try
        {
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            Console.WriteLine("\n" + $"Process bound to port no. {port}. Use `help` to see list of commands." + "\n");
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine("\n" + "Port in use by another process." + "\n");
                return;
            }
        }

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

            // Workaround to poll for command line input, since C# Select is scoped to the `Socket` type.
            if (Console.KeyAvailable)
            {
                string input = Console.ReadLine() ?? string.Empty;

                // Adapted from slide 42, Chapter 2 (tokenizing command input).
                string[] argValues = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                string command = argValues[0];

                if (command == "exit")
                {
                    // Close all connections.
                    for (int i = connections.Count - 1; i >= 0; i--)
                    {
                        connections[i].Close();
                        connections.RemoveAt(i);
                    }
                    break;
                }

                // Invoke methods based on command input.
                switch (command.ToLower())
                {
                    case "help": DisplayHelp(); break;
                    case "myip": DisplayMyIp(); break;
                    case "myport": DisplayMyPort(listener); break;
                    case "connect": EstablishConnection(listener, connections, argValues); break;
                    case "list": ListConnections(connections); break;
                    case "terminate": TerminateConnection(connections, argValues); break;
                    case "send": SendMessage(connections, argValues); break;
                    default:
                        Console.WriteLine("\n" + "Unrecognized command. Use `help` to see list of commands." + "\n");
                        break;
                }
            }
        }
    }

    internal static void HandleSocketActivity(Socket socket, Socket listener, List<Socket> connections)
    {
        if (socket == listener)
        {
            var client = listener.Accept();
            connections.Add(client);
            var remote = client.RemoteEndPoint as IPEndPoint;
            Console.WriteLine("\n" + $"Connection established with host at IP {remote!.Address}, Port No. {remote.Port}" + "\n");
            return;
        }

        // At least 400 bytes to safely hold 100 UTF-8 characters.
        var buffer = new byte[1024];

        try
        {
            var received = socket.Receive(buffer);

            if (received == 0) // Socket closure.
            {
                connections.Remove(socket);
                var remote = socket.RemoteEndPoint as IPEndPoint;
                Console.WriteLine("\n" + $"Connection closed with host at IP {remote!.Address}, Port No. {remote.Port}" + "\n");
                socket.Close();
            }
            else // If has contents.
            {
                var msg = Encoding.UTF8.GetString(buffer, 0, received);
                var sender = socket.RemoteEndPoint as IPEndPoint;

                Console.WriteLine("\n" + $"Message received from {sender!.Address}");
                Console.WriteLine($"Sender's Port: {sender.Port}");
                Console.WriteLine($"Message: \"{msg}\"" + "\n");
            }
        }
        // Handle remote host shutdown WITHOUT exit command.
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {   
            connections.Remove(socket);
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
        Console.WriteLine("\n" + $"IPv4: {ipAddress}" + "\n");
    }

    internal static void DisplayMyPort(Socket listener)
    {
        var endpoint = listener.LocalEndPoint as IPEndPoint;
        Console.WriteLine("\n" + $"Process runs on port no. {endpoint!.Port}" + "\n");
    }

    internal static void EstablishConnection(Socket listener, List<Socket> connections, string[] argValues)
    {
        // Check whether all arguments supplied.
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

        // Check for valid port.
        if (!int.TryParse(argValues[2], out int port) || (port is < 0 or > 65535))
        {
            Console.WriteLine("\n" + "Invalid port no." + "\n");
            return;
        }

        // Check for self-connections.
        if (port == (listener.LocalEndPoint as IPEndPoint)!.Port)
        {
            Console.WriteLine("\n" + "Self-connections not allowed." + "\n");
            return;
        }

        var endpoint = new IPEndPoint(ip, port);

        // Check if there is an existing connection with the same client IP and port combination.
        if (connections.Any(c => (c.RemoteEndPoint as IPEndPoint)!.Equals(endpoint)))
        {
            Console.WriteLine("\n" + "Duplicate connections not allowed." + "\n");
            return;
        }

        // Attempt connection with supplied IP and port combination.
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);
            Console.WriteLine("\n" + $"Successful connection to host at IP {ip}, Port No. {port}" + "\n");
            connections.Add(socket);
        }
        catch (SocketException) // If Connect fails.
        {
            Console.WriteLine("\n" + $"Failed to connect." + "\n");
        }
    }

    internal static void ListConnections(List<Socket> connections)
    {
        Console.WriteLine("\n" + "{0,4}{1,-15}{2,8}", "ID: ", "IP Address", "Port No.");

        for (int i = 0; i < connections.Count; i++)
        {
            Console.WriteLine(
                "{0,4}{1,-15}{2,8}",
                i + 1 + ": ",
                (connections[i].RemoteEndPoint as IPEndPoint)!.Address,
                (connections[i].RemoteEndPoint as IPEndPoint)!.Port);
        }

        Console.Write("\n");
    }

    internal static void TerminateConnection(List<Socket> connections, string[] argValues)
    {
        // Check whether all arguments supplied.
        if (argValues.Length < 2)
        {
            Console.WriteLine("\n" + "Usage: terminate <connection id>" + "\n");
            return;
        }

        if (int.TryParse(argValues[1], out var terminateId))
        {
            // Validate ID to be within range of existing connection IDs.
            if (terminateId < 1 || terminateId > connections.Count)
            {
                Console.WriteLine("\n" + $"No connection with ID: {terminateId}" + "\n");
            }
            else // If valid.
            {
                connections[terminateId - 1].Close();
                Console.WriteLine("\n" + $"Terminated connection with ID: {terminateId}" + "\n");
                connections.RemoveAt(terminateId - 1);
            }
        }
        else // If invalid ID.
        {
            Console.WriteLine("\n" + "Usage: terminate <connection id>" + "\n");
        }
    }

    internal static void SendMessage(List<Socket> connections, string[] argValues)
    {
        // Check whether all arguments supplied and are valid.
        if (argValues.Length < 3 || !int.TryParse(argValues[1], out var id))
        {
            Console.WriteLine("\n" + "Usage: send <id> <message>" + "\n");
            return;
        }

        // Check if connection exists with supplied ID.
        if (id < 1 || id > connections.Count)
        {
            Console.WriteLine("\n" + $"No connection with ID: {id}" + "\n");
            return;
        }

        // Reconstruct whitespace of message, taken as delimiter.
        var msgStr = string.Join(" ", argValues.Skip(2));

        if (msgStr.Length > 100)
        {
            Console.WriteLine("\n" + "Message can only be 100 characters long, including whitespace." + "\n");
            return;
        }

        try
        {
            // Offset by one b/c ID autoincrements starting from 1.
            var sock = connections[id - 1];

            var msgBytes = Encoding.UTF8.GetBytes(msgStr);
            _ = sock.Send(msgBytes);
            Console.WriteLine("\n" + $"Message sent to host with connection ID: {id}." + "\n");
        }
        catch (Exception)
        {
            Console.WriteLine("\n" + $"Failed to send message." + "\n");
        }
    }
}
