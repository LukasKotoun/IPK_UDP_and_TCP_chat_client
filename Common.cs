namespace ipk24chat_client;

public enum State
{
    start,
    auth,
    authReplyWait,
    open,
    joinReplyWait,
    err,
    end
}
public enum MessageType
{
    CONFIRM = 0x00,
    REPLY = 0x01,
    AUTH = 0x02,
    JOIN = 0x03,
    MSG = 0x04,
    ERR = 0xfe,
    BYE = 0xff,
}

public enum CommandType
{
    AUTH,
    JOIN,
    RENAME,
    HELP,
    MSG
}

public class RefWrapper<T>
{
    public T Value { get; set; }

    public RefWrapper(T value)
    {
        Value = value;
    }
}

public static class MessageCharSets
{
    public static string UserNameGroupName = "userName";
    public static string DisplayNameGroupName = "displayName";
    public static string SecretGroupName = "secret";
    public static string ChannelIdGroupName = "channelId";
    public static string MessageContentGroupName = "messageContent";
    
    public static string UserNamePattern { get; } = $@"(?<{UserNameGroupName}>[A-Za-z0-9-]{{1,20}})";
    public static string DisplayNamePattern { get; } = $@"(?<{DisplayNameGroupName}>[\x21-\x7E]{{1,20}})"; 
    public static string SecretPattern { get; } = $@"(?<{SecretGroupName}>[A-Za-z0-9-]{{1,128}})";
    public static string ChannelIdPattern { get; } = $@"(?<{ChannelIdGroupName}>[A-Za-z0-9-.]{{1,20}})";
    public static string MessageContentPattern { get; } = $@"(?<{MessageContentGroupName}>[\x20-\x7E]{{1,1400}})";
}