using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ipk24chat_client;

public interface IChat
{
    public int StartClient();
    public void HandleSigintEnd();
}

/// <summary>
/// Providing base implementation for chat functionalities
/// </summary>
/// <typeparam name="TMessageDataType">The type of message data</typeparam>
public abstract class ChatBase<TMessageDataType> : IChat
{
    //variable decl
    protected readonly string _serverIpAddress;
    protected ushort _serverPortNumber;

    protected int _currState;
    protected int _returnCode;
    protected ushort _currMessageId;
    protected string _displayName;

    protected CancellationTokenSource _readStdInputCancellationTokenSource;
    protected CancellationTokenSource _processStdInputCancellationTokenSource;
    protected CancellationTokenSource _receiveServerMessagesCancellationTokenSource;
    protected CancellationTokenSource _processServerMessagesCancellationTokenSource;

    protected BlockingCollection<string> _userInputBuffer;
    protected BlockingCollection<TMessageDataType> _serverMessagesBuffer;
    protected BlockingCollection<Message> _clientAppMessagesBuffer;
    protected static AutoResetEvent _stateChangedSignal = new(false);

    protected ChatBase(string serverIpAddress, ushort serverPortNumber)
    {
        _serverIpAddress = serverIpAddress;
        _serverPortNumber = serverPortNumber;
        //var init
        _displayName = "displayName";
        _userInputBuffer = new();
        _serverMessagesBuffer = new();
        _clientAppMessagesBuffer = new();
        _currState = (int)State.start;
        _currMessageId = 0;
        _readStdInputCancellationTokenSource = new();
        _processStdInputCancellationTokenSource = new();
        _receiveServerMessagesCancellationTokenSource = new();
        _processServerMessagesCancellationTokenSource = new();
        _returnCode = 0;
    }


    public abstract int StartClient();
    
    //state changing must be before other actions
    public void HandleSigintEnd()
    {
        if (new[] { State.end, State.err }.Contains((State)_currState))
            return;
        Interlocked.Exchange(ref _currState, (int)State.end);
        _readStdInputCancellationTokenSource.Cancel();
        _processStdInputCancellationTokenSource.Cancel();
        _stateChangedSignal.Set();
        if (!_clientAppMessagesBuffer.IsAddingCompleted)
            _clientAppMessagesBuffer.Add(MessageFactory.CreateBye(_currMessageId++));
        _clientAppMessagesBuffer.CompleteAdding();
        _returnCode = 0;
    }

    protected void HandleFileEnd()
    {
        if (_readStdInputCancellationTokenSource.Token.IsCancellationRequested ||
            _clientAppMessagesBuffer.IsAddingCompleted)
            return;
        _readStdInputCancellationTokenSource.Cancel();
        if (!_clientAppMessagesBuffer.IsAddingCompleted)
            _clientAppMessagesBuffer.Add(MessageFactory.CreateBye(_currMessageId++));
        _clientAppMessagesBuffer.CompleteAdding();
        _returnCode = 0;
    }
    
    //state changing must be before other actions
    protected void HandleIncomingError(string errorFrom, string errMessageContent)
    {
        if (new[] { State.end, State.err }.Contains((State)_currState))
            return;
        Interlocked.Exchange(ref _currState, (int)State.err);
        _readStdInputCancellationTokenSource.Cancel();
        _processStdInputCancellationTokenSource.Cancel();
        _stateChangedSignal.Set();
        if (!_clientAppMessagesBuffer.IsAddingCompleted)
            _clientAppMessagesBuffer.Add(MessageFactory.CreateBye(_currMessageId++));
        _clientAppMessagesBuffer.CompleteAdding();
        Console.Error.WriteLine($"ERR FROM {errorFrom}: {errMessageContent}");
        _returnCode = 2;
    }
    
    //state changing must be before other actions
    protected void HandleInternalError(string errMessageContent)
    {
        if (new[] { State.end, State.err }.Contains((State)_currState))
            return;
        Interlocked.Exchange(ref _currState, (int)State.err);
        _readStdInputCancellationTokenSource.Cancel();
        _processStdInputCancellationTokenSource.Cancel();
        _stateChangedSignal.Set();
        if (!_clientAppMessagesBuffer.IsAddingCompleted)
        {
            _clientAppMessagesBuffer.Add(MessageFactory.CreateErr(_currMessageId++, _displayName, errMessageContent));
            _clientAppMessagesBuffer.Add(MessageFactory.CreateBye(_currMessageId++));
        }

        _clientAppMessagesBuffer.CompleteAdding();
        Console.Error.WriteLine($"ERR: {errMessageContent}");
        _returnCode = 3;
    }

    //state changing must be before other actions
    protected void HandleSocketError()
    {
        if (new[] { State.end, State.err }.Contains((State)_currState))
            return;
        Interlocked.Exchange(ref _currState, (int)State.err);
        _readStdInputCancellationTokenSource.Cancel();
        _processStdInputCancellationTokenSource.Cancel();
        _stateChangedSignal.Set();
        _clientAppMessagesBuffer.CompleteAdding();
        Console.Error.WriteLine("ERR: connection lost");
        _returnCode = 11;
    }

    //state changing must be before other actions
    protected void HandleSocketConnectingError()
    {
        if (new[] { State.end, State.err }.Contains((State)_currState))
            return;
        Interlocked.Exchange(ref _currState, (int)State.err);
        _stateChangedSignal.Set();
        Console.Error.WriteLine("ERR: socket connecting failed");
        DisposeResources();
        _returnCode = 10;
    }

    protected virtual void DisposeResources()
    {
        _userInputBuffer.Dispose();
        _serverMessagesBuffer.Dispose();
        _clientAppMessagesBuffer.Dispose();
        _stateChangedSignal.Dispose();
    }

    /// <summary>
    /// Processes the client buffer (buffer for storing mainly err and bye messages)
    /// by sending messages to the server
    /// </summary>
    /// <typeparam name="TSocketClient">The type of the socket client (tcp/udp)</typeparam>
    /// <param name="socketManager">Socket for sending</param>
    protected void ProcessClientBuffer<TSocketClient>(
        SocketManager<TSocketClient, TMessageDataType> socketManager)
    {
        try
        {
            foreach (var message in _clientAppMessagesBuffer.GetConsumingEnumerable())
            {
                socketManager.SendMessage(message.CodeMessage<TMessageDataType>(), message.MessageId);
            }
        }
        catch (Exception ex) when (ex is TimeoutException or SocketException)
        {
        }
    }

    /// <summary>
    /// Reads standard input and add each line to the provided collection
    /// </summary>
    /// <param name="collection">The collection to add input</param>
    /// <param name="cancellationToken">Cancellation token to stop the input reading</param>
    protected async Task ReadStdInput(BlockingCollection<string> collection, CancellationToken cancellationToken)
    {
        using (var reader = new StreamReader(Console.OpenStandardInput()))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? inputLine;
                try
                {
                    inputLine = await reader.ReadLineAsync().WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (inputLine == null)
                {
                    HandleFileEnd();
                    break;
                }

                if (inputLine == "")
                    continue;
                collection.Add(inputLine);
            }
        }

        collection.CompleteAdding();
    }

    /// <summary>
    /// Processes the user input buffer by executing commands and sending messages to the server
    /// </summary>
    /// <typeparam name="TSocketClient">The type of the socket client (tcp/udp)</typeparam>
    /// <param name="socketManager">Socket for sending</param>
    /// <param name="cancellationToken">Token for cancelling waiting for </param>
    protected async Task ProccessUserInput<TSocketClient>(
        SocketManager<TSocketClient, TMessageDataType> socketManager,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var line in _userInputBuffer.GetConsumingEnumerable(cancellationToken))
            {
                //stop processing while waiting for reply
                while (new[] { State.authReplyWait, State.joinReplyWait }.Contains(
                           (State)_currState) && !cancellationToken.IsCancellationRequested)
                {
                    WaitHandle.WaitAny(new WaitHandle[] { _stateChangedSignal, cancellationToken.WaitHandle });
                }

                //err from outside
                if (State.err == (State)_currState || State.end == (State)_currState ||
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                UserCommand currUserCommand;
                try
                {
                    currUserCommand = UserCommandFactory.CreateCommandFromUserInput(line);
                }
                catch (FormatException)
                {
                    await Console.Error.WriteLineAsync(
                        "ERR: trying to process an unknown or otherwise malformed command");
                    continue;
                }

                //send message or execute local command
                switch (currUserCommand.CommType)
                {
                    case CommandType.HELP:
                        currUserCommand.Execute();
                        break;
                    case CommandType.RENAME:
                        currUserCommand.Execute(ref _displayName);
                        break;
                    case CommandType.AUTH:
                    {
                        if (!new[] { State.auth, State.start }.Contains((State)_currState))
                        {
                            await Console.Error.WriteLineAsync("ERR: Cannot use this action again in current state");
                            continue;
                        }

                        Interlocked.Exchange(ref _currState, (int)State.authReplyWait);
                        try
                        {
                            currUserCommand.Execute(socketManager, _currMessageId++, ref _displayName);
                        }
                        catch (Exception ex) when (ex is TimeoutException or SocketException)
                        {
                            HandleSocketError();
                        }

                        break;
                    }
                    case CommandType.JOIN:
                    {
                        if (State.open != (State)_currState)
                        {
                            await Console.Error.WriteLineAsync("ERR: trying to join a channel in non-open state");
                            continue;
                        }

                        Interlocked.Exchange(ref _currState, (int)State.joinReplyWait);
                        try
                        {
                            currUserCommand.Execute(socketManager, _currMessageId++, _displayName);
                        }
                        catch (Exception ex) when (ex is TimeoutException or SocketException)
                        {
                            HandleSocketError();
                        }

                        break;
                    }
                    case CommandType.MSG:
                    {
                        if (!new[] { State.open, State.end }.Contains((State)_currState))
                        {
                            await Console.Error.WriteLineAsync("ERR: trying to send a message in non-open state");
                            continue;
                        }

                        try
                        {
                            currUserCommand.Execute(socketManager, _currMessageId++, _displayName);
                        }
                        catch (Exception ex) when (ex is TimeoutException or SocketException)
                        {
                            HandleSocketError();
                        }

                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Processes message received from server
    /// </summary>
    /// <param name="message">Received message</param>
    protected void ProcessServerMessage(Message message)
    {
        if (!checkStateAndServerMessageCompatibility((State)_currState, message.MessType))
        {
            HandleInternalError("unexpeced message type");
            return;
        }

        switch (message.MessType)
        {
            //state and message are compatible - process auth and based on that change state
            case (MessageType.REPLY):
            {
                //if recived unexpected reply - ignore
                if (!(new[] { State.auth, State.authReplyWait, State.joinReplyWait })
                    .Contains((State)_currState))
                    break;
                MessageReply messageReply = (MessageReply)message;
                if (messageReply.Result)
                {
                    Console.Error.WriteLine($"Success: {messageReply.MessageContent}");
                    Interlocked.Exchange(ref _currState, (int)State.open);
                }
                else
                {
                    Console.Error.WriteLine($"Failure: {messageReply.MessageContent}");
                    if ((State)_currState == State.joinReplyWait)
                        Interlocked.Exchange(ref _currState, (int)State.open);
                    else
                        Interlocked.Exchange(ref _currState, (int)State.auth);
                }

                _stateChangedSignal.Set();
                break;
            }
            case (MessageType.MSG):
            {
                MessageMsg messageMsg = (MessageMsg)message;
                Console.WriteLine($"{messageMsg.DisplayName}: {messageMsg.MessageContent}");
                break;
            }
            case (MessageType.ERR):
            {
                MessageErr messageErr = (MessageErr)message;
                HandleIncomingError(messageErr.DisplayName, messageErr.MessageContent);
                break;
            }
            case (MessageType.BYE):
            {
                if (new[] { State.end, State.err }.Contains((State)_currState))
                    break;
                Interlocked.Exchange(ref _currState, (int)State.end);
                _readStdInputCancellationTokenSource.Cancel();
                _processStdInputCancellationTokenSource.Cancel();
                _stateChangedSignal.Set();
                _clientAppMessagesBuffer.CompleteAdding();
                break;
            }
        }
    }

    /// <summary>
    /// Checks compatibility between the current state and the server message type
    /// </summary>
    /// <param name="state">The current state of the chat</param>
    /// <param name="type">The type of server message received</param>
    /// <returns>If the state and message type are compatible return true otherwise false</returns>
    protected static bool checkStateAndServerMessageCompatibility(State state, MessageType type)
    {
        switch (state)
        {
            case State.start:
                return false;
            case State.auth:
            case State.authReplyWait:
                return (new[] { MessageType.CONFIRM, MessageType.REPLY, MessageType.ERR }
                    .Contains(type));
            case State.open:
            case State.joinReplyWait:
                return (new[]
                        { MessageType.CONFIRM, MessageType.MSG, MessageType.REPLY, MessageType.ERR, MessageType.BYE }
                    .Contains(type));
            case State.err:
                return MessageType.CONFIRM == type;
            case State.end:
                return true;
            default:
                return false;
        }
    }
}