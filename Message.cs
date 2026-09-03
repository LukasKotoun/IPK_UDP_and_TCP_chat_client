using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ipk24chat_client;

public interface IMessage
{
    public MessageType MessType { get; set; }
    public ushort MessageId { get; set; }

    /// <summary>
    /// Encodes the message into a byte array
    /// </summary>
    /// <returns>The byte array representation of the message</returns>
    public byte[] CodeMessageToBytes();

    /// <summary>
    /// Encodes the message into a string without termination character
    /// Termination character will be added while sending
    /// </summary>
    /// <returns>The string representation of the message</returns>
    public string CodeMessageToString();

    /// <summary>
    /// Decodes the message from a coded byte array
    /// Decoded data are stored to instance variables
    /// </summary>
    /// <param name="codedMessage">The coded byte array</param>
    public void DecodeMessage(byte[] codedMessage);

    /// <summary>
    /// Decodes the message from a coded string without termination character
    /// Termination character was removed in message reciving
    /// Decoded data are stored to instance variables
    /// </summary>
    /// <param name="codedMessage">The coded string</param>
    public void DecodeMessage(string codedMessage);
}

public abstract class Message : IMessage
{
    protected const byte ByteMessageStringTermination = 0;
    public ushort MessageId { get; set; }
    public MessageType MessType { get; set; }

    /// <inheritdoc />
    public abstract byte[] CodeMessageToBytes();

    /// <inheritdoc />
    public abstract void DecodeMessage(byte[] codedMessage);

    /// <inheritdoc />
    public abstract string CodeMessageToString();

    /// <inheritdoc />
    public abstract void DecodeMessage(string codedMessage);

    /// <summary>
    /// Code message to byte array or string based on TMessageDataType type
    /// </summary>
    /// <typeparam name="TMessageDataType">Type to witch code</typeparam>
    /// <returns>Coded data</returns>
    public TMessageDataType CodeMessage<TMessageDataType>()
    {
        if (typeof(TMessageDataType) == typeof(byte[]))
            return (TMessageDataType)(object)CodeMessageToBytes();
        return (TMessageDataType)(object)CodeMessageToString();
    }

    /// <summary>
    /// From coded byte message get type of that message
    /// </summary>
    /// <param name="codedMessage">Coded byte message</param>
    /// <returns>Type of message - MessageType enum</returns>
    /// <exception cref="FormatException">Message type is unknown throw FormatException</exception>
    public static MessageType DecodeMessageType(byte[] codedMessage)
    {
        if (codedMessage.Length == 0)
            throw new FormatException();
        byte messageType = codedMessage[0];
        if (Enum.IsDefined(typeof(MessageType), (MessageType)messageType))
        {
            return (MessageType)messageType;
        }

        throw new FormatException();
    }

    /// <summary>
    /// From coded string message get type of that message
    /// </summary>
    /// <param name="codedMessage">Coded string message</param>
    /// <returns>Type of message - MessageType enum</returns>
    /// <exception cref="FormatException">Message type is unknown throw FormatException</exception>
    public static MessageType DecodeMessageType(string codedMessage)
    {
        const string messageErr = @"^ERR ";
        const string messageReply = @"^REPLY ";
        const string messageAuth = @"^AUTH ";
        const string messageJoin = @"^JOIN ";
        const string messageMsg = @"^MSG ";
        const string messageBye = @"^BYE$";
        if (Regex.IsMatch(codedMessage, messageErr, RegexOptions.IgnoreCase)) return MessageType.ERR;
        if (Regex.IsMatch(codedMessage, messageReply, RegexOptions.IgnoreCase)) return MessageType.REPLY;
        if (Regex.IsMatch(codedMessage, messageAuth, RegexOptions.IgnoreCase)) return MessageType.AUTH;
        if (Regex.IsMatch(codedMessage, messageJoin, RegexOptions.IgnoreCase)) return MessageType.JOIN;
        if (Regex.IsMatch(codedMessage, messageMsg, RegexOptions.IgnoreCase)) return MessageType.MSG;
        if (Regex.IsMatch(codedMessage, messageBye, RegexOptions.IgnoreCase)) return MessageType.BYE;
        throw new FormatException();
    }

    /// <summary>
    /// Extracts a string from a byte array starting from the specified index,
    /// until it encounters a null terminator (0 byte).
    /// </summary>
    /// <param name="data">Byte array from with extract string</param>
    /// <param name="index">Index of string start</param>
    /// <returns>Extracted string</returns>
    /// <exception cref="FormatException">Null termination cant be find in data after index</exception>
    protected static string ExtractStringFromBytes(byte[] data, ref int index)
    {
        int startIndex = index;
        index = Array.FindIndex(data, startIndex, b => b == 0);
        if (index == -1)
        {
            throw new FormatException();
        }

        string extractedString = Encoding.UTF8.GetString(data, startIndex, index - startIndex);
        index++; // Move index past the null terminator
        return extractedString;
    }
}

/// <summary>
/// Represents confirmation message for udp confirming
/// </summary>
public class MessageConfirm : Message
{
    public MessageConfirm(ushort messageId)
    {
        MessType = MessageType.CONFIRM;
        MessageId = messageId;
    }

    public MessageConfirm(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.CONFIRM;
    }

    public MessageConfirm(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.CONFIRM;
    }

    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+
        // |  0x00  |  Ref_MessageID  |
        // +--------+--------+--------+

        byte[] codedRefMessageId =
            BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId!));

        int messageByteLength = 1 + codedRefMessageId.Length;
        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.CONFIRM;
        //message ref id
        Array.Copy(codedRefMessageId, 0, codedMessage, index, codedRefMessageId.Length);

        return codedMessage;
    }

    ///tcp doesnt have confirm
    public override string CodeMessageToString()
    {
        return "";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+
        // |  0x00  |  Ref_MessageID  |
        // +--------+--------+--------+
        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.CONFIRM) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
        }
        catch (Exception)
        {
            throw new FormatException();
        }
    }

    ///tcp doesnt have confirm
    public override void DecodeMessage(string codedMessageString)
    {
    }
}

/// <summary>
/// Represents reply message for join on auth confirm/decline  
/// </summary>
public class MessageReply : Message
{
    public bool Result = false;
    public ushort RefMessageId = 0;
    public string MessageContent = "";

    private readonly string CodedMessageStringPattern =
        $@"^REPLY (?<result>OK|NOK) IS {MessageCharSets.MessageContentPattern}$";

    public MessageReply(ushort messageId, bool result, ushort refMessageId, string messageContent)
    {
        if (messageContent == "")
            throw new ArgumentException();
        MessType = MessageType.REPLY;
        MessageId = messageId;
        Result = result;
        RefMessageId = refMessageId;
        MessageContent = messageContent;
    }

    public MessageReply(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.REPLY;
    }

    public MessageReply(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.REPLY;
    }


    public override byte[] CodeMessageToBytes()
    {
        //   1 byte       2 bytes       1 byte       2 bytes      
        // +--------+--------+--------+--------+--------+--------+--------~~---------+---+
        // |  0x01  |    MessageID    | Result |  Ref_MessageID  |  MessageContents  | 0 |
        // +--------+--------+--------+--------+--------+--------+--------~~---------+---+
        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));
        byte[] codedRefMessageId =
            BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)RefMessageId));
        byte[] codedMessageContent = Encoding.ASCII.GetBytes(MessageContent);

        int messageByteLength = 1 + codedMessageId.Length + 1 + codedRefMessageId.Length +
                                codedMessageContent.Length + 1;

        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.REPLY;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);
        index += codedMessageId.Length;
        //result
        codedMessage[index++] = Result ? (byte)1 : (byte)0;
        //ref message id 
        Array.Copy(codedRefMessageId, 0, codedMessage, index, codedRefMessageId.Length);
        index += codedRefMessageId.Length;
        //message content
        Array.Copy(codedMessageContent, 0, codedMessage, index, codedMessageContent.Length);
        index += codedMessageContent.Length;
        codedMessage[index] = ByteMessageStringTermination;

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //REPLY {"OK"|"NOK"} IS {MessageContent}
        return $"REPLY {(Result ? "OK" : "NOK")} IS {MessageContent}";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //   1 byte       2 bytes       1 byte       2 bytes      
        // +--------+--------+--------+--------+--------+--------+--------~~---------+---+
        // |  0x01  |    MessageID    | Result |  Ref_MessageID  |  MessageContents  | 0 |
        // +--------+--------+--------+--------+--------+--------+--------~~---------+---+
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.REPLY) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            Result = codedMessage[index++] == 1;
            RefMessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            MessageContent = ExtractStringFromBytes(codedMessage, ref index);
        }
        catch (Exception)
        {
            throw new FormatException();
        }

        if (!Regex.IsMatch(MessageContent, MessageCharSets.MessageContentPattern)) throw new FormatException();
    }

    public override void DecodeMessage(string codedMessage)
    {
        Match match;
        if ((match = Regex.Match(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase)).Success)
        {
            Result = match.Groups["result"].Value.ToUpper() == "OK";
            MessageContent = match.Groups[MessageCharSets.MessageContentGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

/// <summary>
/// Represents auth message for authentication on server 
/// </summary>
public class MessageAuth : Message
{
    public string UserName = "";
    public string DisplayName = "";
    public string Secret = "";

    private readonly string CodedMessageStringPattern =
        $@"^AUTH {MessageCharSets.UserNamePattern} AS {MessageCharSets.DisplayNamePattern} USING {MessageCharSets.SecretPattern}$";

    public MessageAuth(ushort messageId, string userName, string displayName, string secret)
    {
        if (userName == "" || displayName == "" || secret == "")
            throw new ArgumentException();
        MessType = MessageType.AUTH;
        MessageId = messageId;
        UserName = userName;
        DisplayName = displayName;
        Secret = secret;
    }

    public MessageAuth(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.AUTH;
    }

    public MessageAuth(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.AUTH;
    }


    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+----~~----+---+
        // |  0x02  |    MessageID    |  Username  | 0 |  DisplayName  | 0 |  Secret  | 0 |
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+----~~----+---+
        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));
        byte[] codedUserName = Encoding.ASCII.GetBytes(UserName);
        byte[] codedDisplayName = Encoding.ASCII.GetBytes(DisplayName);
        byte[] codedSecret = Encoding.ASCII.GetBytes(Secret);

        int messageByteLength = 1 + codedMessageId.Length + codedUserName.Length + 1 +
                                codedDisplayName.Length + 1
                                + codedSecret.Length + 1;
        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.AUTH;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);
        index += codedMessageId.Length;
        //username
        Array.Copy(codedUserName, 0, codedMessage, index, codedUserName.Length);
        index += codedUserName.Length;
        codedMessage[index++] = ByteMessageStringTermination;
        //display name
        Array.Copy(codedDisplayName, 0, codedMessage, index, codedDisplayName.Length);
        index += codedDisplayName.Length;
        codedMessage[index++] = ByteMessageStringTermination;
        //secret
        Array.Copy(codedSecret, 0, codedMessage, index, codedSecret.Length);
        index += codedSecret.Length;
        codedMessage[index] = ByteMessageStringTermination;

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //AUTH {Username} AS {DisplayName} USING {Secret}
        return $"AUTH {UserName} AS {DisplayName} USING {Secret}";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+----~~----+---+
        // |  0x02  |    MessageID    |  Username  | 0 |  DisplayName  | 0 |  Secret  | 0 |
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+----~~----+---+
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.AUTH) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            UserName = ExtractStringFromBytes(codedMessage, ref index);
            DisplayName = ExtractStringFromBytes(codedMessage, ref index);
            Secret = ExtractStringFromBytes(codedMessage, ref index);
        }
        catch (Exception)
        {
            throw new FormatException();
        }

        if (!Regex.IsMatch(UserName, MessageCharSets.UserNamePattern) ||
            !Regex.IsMatch(DisplayName, MessageCharSets.DisplayNamePattern) ||
            !Regex.IsMatch(Secret, MessageCharSets.SecretPattern))
            throw new FormatException();
    }

    public override void DecodeMessage(string codedMessage)
    {
        Match match;
        if ((match = Regex.Match(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase)).Success)
        {
            UserName = match.Groups[MessageCharSets.UserNameGroupName].Value;
            DisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
            Secret = match.Groups[MessageCharSets.SecretGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

/// <summary>
/// Represents join message for channel changing
/// </summary>
public class MessageJoin : Message
{
    public string ChannelId = "";
    public string DisplayName = "";

    private readonly string CodedMessageStringPattern =
        $@"^JOIN {MessageCharSets.ChannelIdPattern} AS {MessageCharSets.DisplayNamePattern}$";

    public MessageJoin(ushort messageId, string channelId, string displayName)
    {
        if (channelId == "" || displayName == "")
            throw new ArgumentException();
        MessType = MessageType.JOIN;
        MessageId = messageId;
        ChannelId = channelId;
        DisplayName = displayName;
    }

    public MessageJoin(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.JOIN;
    }

    public MessageJoin(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.JOIN;
    }

    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+
        // |  0x03  |    MessageID    |  ChannelID | 0 |  DisplayName  | 0 |
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+

        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));
        byte[] codedChannelId = Encoding.ASCII.GetBytes(ChannelId);
        byte[] codedDisplayName = Encoding.ASCII.GetBytes(DisplayName);

        int messageByteLength = 1 + codedMessageId.Length + codedChannelId.Length + 1 + codedDisplayName.Length + 1;
        byte[] codedMessage = new byte [messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.JOIN;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);
        index += codedMessageId.Length;
        //channel id (string)
        Array.Copy(codedChannelId, 0, codedMessage, index, codedChannelId.Length);
        index += codedChannelId.Length;
        codedMessage[index++] = ByteMessageStringTermination;
        //display name
        Array.Copy(codedDisplayName, 0, codedMessage, index, codedDisplayName.Length);
        index += codedDisplayName.Length;
        codedMessage[index] = ByteMessageStringTermination;

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //JOIN {ChannelID} AS {DisplayName}
        return $"JOIN {ChannelId} AS {DisplayName}";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+
        // |  0x03  |    MessageID    |  ChannelID | 0 |  DisplayName  | 0 |
        // +--------+--------+--------+-----~~-----+---+-------~~------+---+
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.JOIN) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            ChannelId = ExtractStringFromBytes(codedMessage, ref index);
            DisplayName = ExtractStringFromBytes(codedMessage, ref index);
        }
        catch (Exception)
        {
            throw new FormatException();
        }

        if (!Regex.IsMatch(ChannelId, MessageCharSets.ChannelIdPattern) ||
            !Regex.IsMatch(DisplayName, MessageCharSets.DisplayNamePattern))
            throw new FormatException();
    }

    public override void DecodeMessage(string codedMessage)
    {
        Match match;
        if ((match = Regex.Match(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase)).Success)
        {
            ChannelId = match.Groups[MessageCharSets.ChannelIdGroupName].Value;
            DisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

/// <summary>
/// Represents normal info message 
/// </summary>
public class MessageMsg : Message
{
    public string DisplayName = "";
    public string MessageContent = "";

    private readonly string CodedMessageStringPattern =
        $@"^MSG FROM {MessageCharSets.DisplayNamePattern} IS {MessageCharSets.MessageContentPattern}$";

    public MessageMsg(ushort messageId, string displayName, string messageContent)
    {
        if (displayName == "" || messageContent == "")
            throw new ArgumentException();
        MessType = MessageType.MSG;
        MessageId = messageId;
        DisplayName = displayName;
        MessageContent = messageContent;
    }

    public MessageMsg(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.MSG;
    }

    public MessageMsg(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.MSG;
    }

    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+
        // |  0x04  |    MessageID    |  DisplayName  | 0 |  MessageContents  | 0 |
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+

        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));
        byte[] codedDisplayName = Encoding.ASCII.GetBytes(DisplayName);
        byte[] codedMessageContent = Encoding.ASCII.GetBytes(MessageContent);

        int messageByteLength = 1 + codedMessageId.Length + codedDisplayName.Length + 1 +
                                codedMessageContent.Length + 1;
        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.MSG;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);
        index += codedMessageId.Length;
        //display name
        Array.Copy(codedDisplayName, 0, codedMessage, index, codedDisplayName.Length);
        index += codedDisplayName.Length;
        codedMessage[index++] = ByteMessageStringTermination;
        //message content
        Array.Copy(codedMessageContent, 0, codedMessage, index, codedMessageContent.Length);
        index += codedMessageContent.Length;
        codedMessage[index] = ByteMessageStringTermination;

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //MSG FROM {DisplayName} IS {MessageContent}
        return $"MSG FROM {DisplayName} IS {MessageContent}";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+
        // |  0x04  |    MessageID    |  DisplayName  | 0 |  MessageContents  | 0 |
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+          
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.MSG) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            DisplayName = ExtractStringFromBytes(codedMessage, ref index);
            MessageContent = ExtractStringFromBytes(codedMessage, ref index);
        }
        catch (Exception)
        {
            throw new FormatException();
        }

        if (!Regex.IsMatch(DisplayName, MessageCharSets.DisplayNamePattern) || 
            !Regex.IsMatch(MessageContent, MessageCharSets.MessageContentPattern))
            throw new FormatException();
    }

    public override void DecodeMessage(string codedMessage)
    {
        Match match;
        if ((match = Regex.Match(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase)).Success)
        {
            DisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
            MessageContent = match.Groups[MessageCharSets.MessageContentGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

/// <summary>
/// Represents error message for error state sharing
/// </summary>
public class MessageErr : Message
{
    public string DisplayName = "";
    public string MessageContent = "";

    private readonly string CodedMessageStringPattern =
        $@"^ERR FROM {MessageCharSets.DisplayNamePattern} IS {MessageCharSets.MessageContentPattern}$";

    public MessageErr(ushort messageId, string displayName, string messageContent)
    {
        if (displayName == "" || messageContent == "")
            throw new ArgumentException();
        MessType = MessageType.ERR;
        MessageId = messageId;
        DisplayName = displayName;
        MessageContent = messageContent;
    }

    public MessageErr(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.ERR;
    }

    public MessageErr(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.ERR;
    }

    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+
        // |  0xFE  |    MessageID    |  DisplayName  | 0 |  MessageContents  | 0 |
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+

        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));
        byte[] codedDisplayName = Encoding.ASCII.GetBytes(DisplayName);
        byte[] codedMessageContent = Encoding.ASCII.GetBytes(MessageContent);

        int messageByteLength = 1 + codedMessageId.Length + codedDisplayName.Length + 1 +
                                codedMessageContent.Length + 1;
        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.ERR;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);
        index += codedMessageId.Length;
        //display name
        Array.Copy(codedDisplayName, 0, codedMessage, index, codedDisplayName.Length);
        index += codedDisplayName.Length;
        codedMessage[index++] = ByteMessageStringTermination;
        //message content
        Array.Copy(codedMessageContent, 0, codedMessage, index, codedMessageContent.Length);
        index += codedMessageContent.Length;
        codedMessage[index] = ByteMessageStringTermination;

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //ERR FROM {DisplayName} IS {MessageContent}
        return $"ERR FROM {DisplayName} IS {MessageContent}";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+
        // |  0xFE  |    MessageID    |  DisplayName  | 0 |  MessageContents  | 0 |
        // +--------+--------+--------+-------~~------+---+--------~~---------+---+
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.ERR) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
            index += 2;
            DisplayName = ExtractStringFromBytes(codedMessage, ref index);
            MessageContent = ExtractStringFromBytes(codedMessage, ref index);
        }
        catch (Exception)
        {
            throw new FormatException();
        }
        if (!Regex.IsMatch(DisplayName, MessageCharSets.DisplayNamePattern) || 
            !Regex.IsMatch(MessageContent, MessageCharSets.MessageContentPattern))
            throw new FormatException();
    }

    public override void DecodeMessage(string codedMessage)
    {
        Match match;
        if ((match = Regex.Match(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase)).Success)
        {
            DisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
            MessageContent = match.Groups[MessageCharSets.MessageContentGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

/// <summary>
/// Represents bye message communication ending
/// </summary>
public class MessageBye : Message
{
    private const string CodedMessageStringPattern =
        @"^BYE$";

    public MessageBye(ushort messageId)
    {
        MessType = MessageType.BYE;
        MessageId = messageId;
    }

    public MessageBye(byte[] codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.BYE;
    }

    public MessageBye(string codedMessage)
    {
        DecodeMessage(codedMessage);
        MessType = MessageType.BYE;
    }

    public override byte[] CodeMessageToBytes()
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+
        // |  0xFF  |    MessageID    |
        // +--------+--------+--------+
        byte[] codedMessageId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)MessageId));

        int messageByteLength = 1 + codedMessageId.Length;
        byte[] codedMessage = new byte[messageByteLength];
        int index = 0;
        //message type
        codedMessage[index++] = (byte)MessageType.BYE;
        //message id
        Array.Copy(codedMessageId, 0, codedMessage, index, codedMessageId.Length);

        return codedMessage;
    }

    public override string CodeMessageToString()
    {
        //BYE
        return $"BYE";
    }

    public override void DecodeMessage(byte[] codedMessage)
    {
        //  1 byte       2 bytes      
        // +--------+--------+--------+
        // |  0xFF  |    MessageID    |
        // +--------+--------+--------+
        if (codedMessage.Length == 0)
            throw new FormatException();

        int index = 0;
        byte messageType = codedMessage[index++];
        if (messageType != (byte)MessageType.BYE) throw new FormatException();
        try
        {
            MessageId = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(codedMessage, index));
        }
        catch (Exception)
        {
            throw new FormatException();
        }
    }

    public override void DecodeMessage(string codedMessage)
    {
        if (!Regex.IsMatch(codedMessage, CodedMessageStringPattern, RegexOptions.IgnoreCase))
        {
            throw new FormatException();
        }
    }
}