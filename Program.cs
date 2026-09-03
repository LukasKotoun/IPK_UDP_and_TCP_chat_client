using System.CommandLine.DragonFruit;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace ipk24chat_client;

internal class Program
{
    /// <summary>
    /// Client for a chat server using IPK24-CHAT protocol,
    /// User input not prefixed with the proper command character shall be interpreted as a message to be sent to the server.
    ///  
    /// Client input commands
    /// /auth {Username} {Secret} {DisplayName} Sends AUTH message with the data provided from the command to the server (and correctly handles the Reply message),
    ///                                         locally sets the DisplayName value (same as the /rename command).
    /// /join {ChannelID}                       Sends JOIN message with channel name from the command to the server (and correctly handles the Reply message)
    /// /rename {DisplayName}                   Locally changes the display name of the user to be sent with new messages/selected commands
    /// /help                                   Prints out supported local commands with their parameters and a description
    /// </summary>
    /// <param name="t">Transport protocol used for connection (tcp or udp)</param>
    /// <param name="s">Server IP or hostname</param>
    /// <param name="p">Server port (4567 default)</param>
    /// <param name="d">UDP confirmation timeout (250ms default)</param>
    /// <param name="r">Maximum number of UDP retransmissions (3 default)</param>
    static int Main(string t = "", string s = "", ushort p = 4567, ushort d = 250, byte r = 3)
    {
        IChat chat;
        if (t == "udp")
            chat = new UdpChat(s, p, d, r);
        else
            chat = new TcpChat(s, p);
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            chat.HandleSigintEnd();
        };
        return chat.StartClient();
    }
}