using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace ipk24chat_client;


/// <summary>
/// Factory class for creating instances of messages
/// </summary>
public static class MessageFactory
{
    /// <summary>
    /// Creating CONFIRM, BYE
    /// </summary>
    public static Message CreateMessageFromArgs(MessageType type, ushort messageIdOrRefMessageId)
    {
        return type switch
        {
            MessageType.CONFIRM => CreateConfirm(messageIdOrRefMessageId),
            MessageType.BYE => CreateBye(messageIdOrRefMessageId),
            _ => throw new ArgumentException()
        };
    }

    /// <summary>
    /// Creating REPLY
    /// </summary>
    public static Message CreateMessageFromArgs(MessageType type, ushort messageId, bool result, ushort refMessageId,
        string messageContent)
    {
        if (type != MessageType.REPLY) throw new ArgumentException();
        return CreateReply(messageId, result, refMessageId, messageContent);
    }

    /// <summary>
    /// Creating AUTH
    /// </summary>
    public static Message CreateMessageFromArgs(MessageType type, ushort messageId, string userName, string displayName,
        string secret)
    {
        if (type != MessageType.AUTH) throw new ArgumentException();
        return CreateAuth(messageId, userName, displayName, secret);
    }

    /// <summary>
    /// Creating JOIN, MSG, ERR
    /// </summary>
    public static Message CreateMessageFromArgs(MessageType type, ushort messageId, string channelOrDisplayName,
        string displayNameOrContent)
    {
        return type switch
        {
            MessageType.JOIN => CreateJoin(messageId, channelOrDisplayName, displayNameOrContent),
            MessageType.MSG => CreateMsg(messageId, channelOrDisplayName, displayNameOrContent),
            MessageType.ERR => CreateErr(messageId, channelOrDisplayName, displayNameOrContent),
            _ => throw new ArgumentException()
        };
    }

    public static Message CreateConfirm(ushort refMessageId)
    {
        return new MessageConfirm(refMessageId);
    }

    public static Message CreateBye(ushort messageId)
    {
        return new MessageBye(messageId);
    }

    public static Message CreateReply(ushort messageId, bool result, ushort refMessageId,
        string messageContent)
    {
        return new MessageReply(messageId, result, refMessageId, messageContent);
    }

    public static Message CreateAuth(ushort messageId, string userName, string displayName,
        string secret)
    {
        return new MessageAuth(messageId, userName, displayName, secret);
    }

    public static Message CreateJoin(ushort messageId, string channelId,
        string displayName)
    {
        return new MessageJoin(messageId, channelId, displayName);
    }

    public static Message CreateMsg(ushort messageId, string displayName, string messageContent)
    {
        return new MessageMsg(messageId, displayName, messageContent);
    }

    public static Message CreateErr(ushort messageId, string displayName, string messageContent)
    {
        return new MessageErr(messageId, displayName, messageContent);
    }

    /// <summary>
    /// Creates a Message object from a coded byte array, based on its message type
    /// </summary>
    /// <param name="codedMessage">The coded byte array representation of the message</param>
    /// <returns>A Message object corresponding to the decoded message type</returns>
    /// <exception cref="FormatException">When the coded message has an invalid format</exception>
    public static Message CreateMessageFromCoded(byte[] codedMessage)
    {
        MessageType? messageType = Message.DecodeMessageType(codedMessage);
        if (messageType == null)
            throw new FormatException();
        return messageType switch
        {
            MessageType.CONFIRM => new MessageConfirm(codedMessage),
            MessageType.REPLY => new MessageReply(codedMessage),
            MessageType.AUTH => new MessageAuth(codedMessage),
            MessageType.JOIN => new MessageJoin(codedMessage),
            MessageType.MSG => new MessageMsg(codedMessage),
            MessageType.ERR => new MessageErr(codedMessage),
            MessageType.BYE => new MessageBye(codedMessage),
            _ => throw new FormatException()
        };
    }
    
    /// <summary>
    /// Creates a Message object from a coded string, based on its message type
    /// </summary>
    /// <param name="codedMessage">The coded string representation of the message</param>
    /// <returns>A Message object corresponding to the decoded message type</returns>
    /// <exception cref="FormatException">When the coded message has an invalid format</exception>
    public static Message CreateMessageFromCoded(string codedMessage)
    {
        MessageType messageType = Message.DecodeMessageType(codedMessage);
        return messageType switch
        {
            MessageType.CONFIRM => new MessageConfirm(codedMessage),
            MessageType.REPLY => new MessageReply(codedMessage),
            MessageType.AUTH => new MessageAuth(codedMessage),
            MessageType.JOIN => new MessageJoin(codedMessage),
            MessageType.MSG => new MessageMsg(codedMessage),
            MessageType.ERR => new MessageErr(codedMessage),
            MessageType.BYE => new MessageBye(codedMessage),
            _ => throw new FormatException()
        };
    }

}

/// <summary>
/// Factory class for creating instances of user command
/// </summary>
public static class UserCommandFactory
{
    
    /// <summary>
    /// Creation AUTH 
    /// </summary>
    public static UserCommand CreateCommandFromArgs(CommandType type, string userName, string secret,
        string displayName)
    {
        if (type != CommandType.AUTH) throw new ArgumentException();
        return CreateAuth(userName, secret, displayName);
    }
    
    /// <summary>
    /// Creating JOIN, RENAME, MSG
    /// </summary>
    public static UserCommand CreateCommandFromArgs(CommandType type, string channelIdOrDisplayNameOrMessage)
    {
        return type switch
        {
            CommandType.JOIN => CreateJoin(channelIdOrDisplayNameOrMessage),
            CommandType.RENAME => CreateRename(channelIdOrDisplayNameOrMessage),
            CommandType.MSG => CreateMsg(channelIdOrDisplayNameOrMessage),
            _ => throw new ArgumentException()
        };
    }
    
    /// <summary>
    /// Creating HELP
    /// </summary>
    public static UserCommand CreateCommandFromArgs(CommandType type)
    {
        if (type != CommandType.HELP) throw new ArgumentException();
        return CreateHelp();
    }

    public static UserCommand CreateAuth(string userName, string secret, string displayName)
    {
        return new UserCommandAuth(userName, secret, displayName);
    }

    public static UserCommand CreateJoin(string channelId)
    {
        return new UserCommandJoin(channelId);
    }

    public static UserCommand CreateRename(string displayName)
    {
        return new UserCommandRename(displayName);
    }

    public static UserCommand CreateMsg(string messageContent)
    {
        return new UserCommandMsg(messageContent);
    }

    public static UserCommand CreateHelp()
    {
        return new UserCommandHelp();
    }
    
    /// <summary>
    /// Creates a UserCommand object from user console input
    /// </summary>
    /// <param name="input">The user input</param>
    /// <returns>A UserCommand object corresponding to the decoded user input</returns>
    /// <exception cref="FormatException">When the user input has an invalid format</exception>
    public static UserCommand CreateCommandFromUserInput(string input)
    {
        CommandType commandType = UserCommand.GetCommandType(input);
        return commandType switch
        {
            CommandType.AUTH => new UserCommandAuth(input),
            CommandType.JOIN => new UserCommandJoin(input),
            CommandType.RENAME => new UserCommandRename(input),
            CommandType.HELP => new UserCommandHelp(input),
            CommandType.MSG => new UserCommandMsg(input),
            _ => throw new FormatException()
        };
    }
}

/// <summary>
/// Factory class for creating instances of socket manager
/// </summary>
public static class SocketManagerFactory
{
    public static SocketManager<UdpClient, byte[]> CreateSocketManager(string serverHostName, ushort serverPortNumber,
        ushort confirmTimeOut, byte maxRetransmissions, AutoResetEvent confirmedMessagesChangedSignal,
        RefWrapper<ushort?> lastConfirmedMessageId,
        AddressFamily listeningIpAddressFamily = AddressFamily.InterNetwork, ushort listeningPort = 0)
    {
        return new UdpSocketMannager(serverHostName, serverPortNumber, confirmTimeOut, maxRetransmissions,
            confirmedMessagesChangedSignal, lastConfirmedMessageId);
    }

    public static SocketManager<TcpClient, string> CreateSocketManager(string serverHostName, ushort serverPortNumber,
        AddressFamily ipAddressFamily = AddressFamily.InterNetwork, string messageTermination = "\r\n")
    {
        return new TcpSocketMannager(serverHostName, serverPortNumber, ipAddressFamily, messageTermination);
    }
}