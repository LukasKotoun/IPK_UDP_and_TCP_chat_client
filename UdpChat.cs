using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ipk24chat_client;

public class UdpChat : ChatBase<byte[]>
{
    private readonly ushort _confirmTimeOut;
    private readonly byte _maxRetransmissions;
    private RefWrapper<ushort?> _lastConfirmedMessageId;
    protected ConcurrentBag<ushort> _recivedMessagesId;
    static AutoResetEvent _confirmedMessagesChangedSignal = new(false);

    public UdpChat(string serverIpAddress, ushort serverPortNumber, ushort confirmTimeOut, byte maxRetransmissions) :
        base(serverIpAddress, serverPortNumber)
    {
        _confirmTimeOut = confirmTimeOut;
        _maxRetransmissions = maxRetransmissions;
        _lastConfirmedMessageId = new(null);
        _recivedMessagesId = new();
    }

    /// <summary>
    /// Start whole client logic  
    /// </summary>
    /// <returns>Return code of application</returns>
    public override int StartClient()
    {
        SocketManager<UdpClient, byte[]> udpSocketManager;
        try
        {
            udpSocketManager = SocketManagerFactory.CreateSocketManager(_serverIpAddress, _serverPortNumber,
                _confirmTimeOut, _maxRetransmissions, _confirmedMessagesChangedSignal, _lastConfirmedMessageId);
        }
        catch (SocketException)
        {
            HandleSocketConnectingError();
            return _returnCode;
        }

        Task readInputTask = Task.Run(() => ReadStdInput(_userInputBuffer, _readStdInputCancellationTokenSource.Token));
        Task processInputTask = Task.Run(() =>
            ProccessUserInput(udpSocketManager, _processStdInputCancellationTokenSource.Token));

        Task receiveServerMessagesTask = Task.Run(() =>
            udpSocketManager.ReceiveServerMessages(_serverMessagesBuffer, HandleSocketError,
                _receiveServerMessagesCancellationTokenSource.Token));
        Task processServerMessagesTask = Task.Run(() =>
            ProcessServerMessages((UdpSocketMannager)udpSocketManager,
                _processServerMessagesCancellationTokenSource.Token));

        Task.WaitAll(readInputTask, processInputTask);

        if (State.err != (State)_currState)
            Interlocked.Exchange(ref _currState, (int)State.end);
        _stateChangedSignal.Set();
        ProcessClientBuffer(udpSocketManager);

        _processServerMessagesCancellationTokenSource.Cancel();
        _receiveServerMessagesCancellationTokenSource.Cancel();
        Task.WaitAll(receiveServerMessagesTask, processServerMessagesTask);
        udpSocketManager.Dispose();
        DisposeResources();
        return _returnCode;
    }

    protected override void DisposeResources()
    {
        _userInputBuffer.Dispose();
        _serverMessagesBuffer.Dispose();
        _clientAppMessagesBuffer.Dispose();
        _stateChangedSignal.Dispose();
        _confirmedMessagesChangedSignal.Dispose();
    }
    
    /// <summary>
    /// Process messages received from server 
    /// </summary>
    /// <param name="socketManager">Socket for sending data</param>
    /// <param name="cancellationToken">Token for stop processing messages</param>
    private Task ProcessServerMessages(UdpSocketMannager socketManager,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var data in _serverMessagesBuffer.GetConsumingEnumerable(cancellationToken))
            {
                Message message;
                try
                {
                    message = MessageFactory.CreateMessageFromCoded(data);
                }
                catch (FormatException)
                {
                    HandleInternalError("invalid message format, cannot decode");
                    continue;
                }

                //process confirm message
                if (message.MessType == MessageType.CONFIRM)
                {
                    //handle unexpected ref message id
                    if (message.MessageId >= _currMessageId)
                    {
                        HandleInternalError("unexpected refMessageId");
                    }
                    else if (_lastConfirmedMessageId.Value == null || message.MessageId > _lastConfirmedMessageId.Value)
                    {
                        _lastConfirmedMessageId.Value = message.MessageId;
                        _confirmedMessagesChangedSignal.Set();
                    }
                    //confirm processed
                    continue;
                }
                
                //send confirm
                try
                {
                    socketManager.SendOneTimeMessage(MessageFactory.CreateConfirm(message.MessageId)
                        .CodeMessageToBytes());
                }
                catch (SocketException)
                {
                    HandleSocketError();
                }

                //skip duplicated messages
                if (_recivedMessagesId.Contains(message.MessageId))
                    continue;
                // save id of non duplicated
                _recivedMessagesId.Add(message.MessageId);
                
                //handle unexpected ref message id
                if (message.MessType == MessageType.REPLY)
                {
                    if (((MessageReply)message).RefMessageId >= _currMessageId)
                    {
                        HandleInternalError("unexpected refMessageId");
                        continue;
                    }
                }

                ProcessServerMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return Task.CompletedTask;
    }
}