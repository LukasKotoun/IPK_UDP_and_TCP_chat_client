using System.Text.RegularExpressions;

namespace ipk24chat_client;

public interface IUserCommand
{
    public CommandType CommType { get; set; }

    public void DecodeUserInput(string userInput)
    {
    }
}

public class UserCommand : IUserCommand
{
    public CommandType CommType { get; set; }
    
    public static CommandType GetCommandType(string input)
    {
        const string commandPattern = @"^/[A-Za-z0-9-_]*";

        const string authCommand = @"^/auth\s+";
        const string joinCommand = @"^/join\s+";
        const string renameCommand = @"^/rename\s+";
        const string helpCommand = @"^/help\s*";
        if (Regex.IsMatch(input, commandPattern))
        {
            if (Regex.IsMatch(input, authCommand, RegexOptions.IgnoreCase)) return CommandType.AUTH;
            if (Regex.IsMatch(input, joinCommand, RegexOptions.IgnoreCase)) return CommandType.JOIN;
            if (Regex.IsMatch(input, renameCommand, RegexOptions.IgnoreCase)) return CommandType.RENAME;
            if (Regex.IsMatch(input, helpCommand, RegexOptions.IgnoreCase)) return CommandType.HELP;
            throw new FormatException();
        }

        return CommandType.MSG;
    }
    
    /// <summary>
    /// HELP execute
    /// </summary>
    public virtual void Execute()
    {
    }

    /// <summary>
    /// RENAME execute
    /// </summary>
    public virtual void Execute(ref string currDisplayName)
    {
    }

    /// <summary>
    /// AUTH execute
    /// </summary>
    public virtual void Execute<TSocketClient, TMessageDataType>(
        SocketManager<TSocketClient, TMessageDataType> socketManager, ushort messageId, ref string displayName)
    {
    }
    //

    /// <summary>
    /// JOIN, MSG execute
    /// </summary>
    public virtual void Execute<TSocketClient, TMessageDataType>(
        SocketManager<TSocketClient, TMessageDataType> socketManager, ushort messageId, string displayName)
    {
    }

    public virtual void DecodeUserInput(string userInput)
    {
    }
}

public class UserCommandAuth : UserCommand
{
    public string UserName = "";
    public string Secret = "";
    public string DisplayName = "";
    
    // /auth {Username} {Secret} {DisplayName}
      private readonly string AuthPattern =
        $@"^/auth\s+{MessageCharSets.UserNamePattern}\s+{MessageCharSets.SecretPattern}\s+{MessageCharSets.DisplayNamePattern}\s*$";

    public UserCommandAuth(string userName, string secret, string displayName)
    {
        if (userName == "" || secret == "" || displayName == "")
            throw new ArgumentException();
        CommType = CommandType.AUTH;
        UserName = userName;
        Secret = secret;
        DisplayName = displayName;
    }

    public UserCommandAuth(string userInput)
    {
        DecodeUserInput(userInput);
        CommType = CommandType.AUTH;
    }

    public override void Execute<TSocketClient, TMessageDataType>(
        SocketManager<TSocketClient, TMessageDataType> socketManager, ushort messageId, ref string displayName)
    {
        displayName = DisplayName;
        Message authMessage =
            MessageFactory.CreateAuth(messageId, UserName, DisplayName, Secret);
        TMessageDataType message = authMessage.CodeMessage<TMessageDataType>();

        socketManager.SendMessage(message, messageId);
    }

    public override void DecodeUserInput(string userInput)
    {
        Match match;
        if ((match = Regex.Match(userInput, AuthPattern, RegexOptions.IgnoreCase)).Success)
        {
            UserName = match.Groups[MessageCharSets.UserNameGroupName].Value;
            Secret = match.Groups[MessageCharSets.SecretGroupName].Value;
            DisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

public class UserCommandJoin : UserCommand
{
    public string ChannelId = "";
    // /join {ChannelID}
    private readonly string JoinPattern = $@"^/join\s+{MessageCharSets.ChannelIdPattern}\s*$";

    public UserCommandJoin(string channelId, string displayName)
    {
        if (channelId == "" || displayName == "")
            throw new ArgumentException();
        CommType = CommandType.JOIN;
        ChannelId = channelId;
    }

    public UserCommandJoin(string userInput)
    {
        DecodeUserInput(userInput);
        CommType = CommandType.JOIN;
    }

    public override void Execute<TSocketClient, TMessageDataType>(
        SocketManager<TSocketClient, TMessageDataType> socketManager, ushort messageId, string displayName)
    {
        Message authMessage =
            MessageFactory.CreateJoin(messageId, ChannelId, displayName);
        TMessageDataType message = authMessage.CodeMessage<TMessageDataType>();

        socketManager.SendMessage(message, messageId);
    }

    public override void DecodeUserInput(string userInput)
    {
        Match match;
        if ((match = Regex.Match(userInput, JoinPattern, RegexOptions.IgnoreCase)).Success)
        {
            ChannelId = match.Groups[MessageCharSets.ChannelIdGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

public class UserCommandRename : UserCommand
{
    public string NewDisplayName = "";
    
    // /rename {DisplayName}
    private readonly string RenamePattern = $@"^/rename\s+{MessageCharSets.DisplayNamePattern}\s*$";
     
    public UserCommandRename(string newDisplayName, bool byParams)
    {
        if (newDisplayName == "")
            throw new ArgumentException();
        CommType = CommandType.RENAME;
        NewDisplayName = newDisplayName;
    }

    public UserCommandRename(string userInput)
    {
        DecodeUserInput(userInput);
        CommType = CommandType.RENAME;
    }

    public override void Execute(ref string currDisplayName)
    {
        currDisplayName = NewDisplayName;
    }

    public override void DecodeUserInput(string userInput)
    {
        Match match;
        if ((match = Regex.Match(userInput, RenamePattern, RegexOptions.IgnoreCase)).Success)
        {
            NewDisplayName = match.Groups[MessageCharSets.DisplayNameGroupName].Value;
        }
        else
        {
            throw new FormatException();
        }
    }
}

public class UserCommandHelp : UserCommand
{
    private const string HelpPattern = @"^/help\s*$";

    public UserCommandHelp()
    {
        CommType = CommandType.HELP;
    }

    public UserCommandHelp(string userInput)
    {
        DecodeUserInput(userInput);
        CommType = CommandType.HELP;
    }

    public override void Execute()
    {
        string help = @"Client for a chat server using IPK24-CHAT protocol,
userInput not prefixed with the proper command character shall be interpreted as a message to be sent to the server.

Client userInput commands
/auth {Username} {Secret} {DisplayName} Sends AUTH message with the data provided from the command to the server 
                                        (and correctly handles the Reply message),
                                        locally sets the DisplayName value (same as the /rename command).
                                        Username - max 20 characters 'A-z0-9-', Secret - max 128 characters 'A-z0-9-', 
                                        DisplayName - max 20 chars  Printable characters (0x21-7E)
/join {ChannelID}                       Sends JOIN message with channel name from the command to the server (and correctly handles the Reply message)
                                        ChannelID - max 20 chars 'A-z0-9-'
/rename {DisplayName}                   Locally changes the display name of the user to be sent with new messages/selected commands
                                        DisplayName - max 20 chars  Printable characters (0x21-7E)
/help                                   Prints out supported local commands with their parameters and a description";
        Console.WriteLine(help);
    }

    public override void DecodeUserInput(string userInput)
    {
        if (!Regex.IsMatch(userInput, HelpPattern, RegexOptions.IgnoreCase)) throw new FormatException();
    }
}

public class UserCommandMsg : UserCommand
{
    public string MessageContent = "";
    private readonly string MsgPattern = $@"^{MessageCharSets.MessageContentPattern}$";

    public UserCommandMsg(string message, bool byParams)
    {
        if (message == "")
            throw new ArgumentException();
        CommType = CommandType.MSG;
        MessageContent = message;
    }

    public UserCommandMsg(string userInput)
    {
        DecodeUserInput(userInput);
        CommType = CommandType.MSG;
    }

    public override void Execute<TSocketClient, TMessageDataType>(
        SocketManager<TSocketClient, TMessageDataType> socketManager, ushort messageId, string displayName)
    {
        Message authMessage =
            MessageFactory.CreateMsg(messageId, displayName, MessageContent);
        TMessageDataType message = authMessage.CodeMessage<TMessageDataType>();

        socketManager.SendMessage(message, messageId);
    }

    public override void DecodeUserInput(string userInput)
    {
        if (Regex.IsMatch(userInput, MsgPattern))
        {
            MessageContent = userInput;
        }
        else
        {
            throw new FormatException();
        }
    }
}