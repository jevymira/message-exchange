using System.Net;
using System.Net.Sockets;

namespace chat
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                string? input = Console.ReadLine();

                if (input == "exit") // #8
                {
                    break;
                }
            }
        }
    }
}
