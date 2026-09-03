using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ipk24chat_client;

public class TcpChat : ChatBase<string>
{
    public TcpChat(string serverIpAddress, ushort serverPortNumber) :
        base(serverIpAddress, serverPortNumber)
    {
    }

    /// <summary>
    /// Start whole client logic  
    /// </summary>
    /// <returns>Return code of application</returns>
    public override int StartClient()
    {
        SocketManager<TcpClient, string> tcpSocketManager;
        try
        {
            tcpSocketManager = SocketManagerFactory.CreateSocketManager(_serverIpAddress, _serverPortNumber);
        }
        catch (SocketException)
        {
            HandleSocketConnectingError();
            return _returnCode;
        }

        Task readInputTask = Task.Run(() => ReadStdInput(_userInputBuffer, _readStdInputCancellationTokenSource.Token));
        Task processInputTask = Task.Run(() =>
            ProccessUserInput(tcpSocketManager, _processStdInputCancellationTokenSource.Token));

        Task receiveServerMessagesTask = Task.Run(() =>
            tcpSocketManager.ReceiveServerMessages(_serverMessagesBuffer, HandleSocketError,
                _receiveServerMessagesCancellationTokenSource.Token)
        );
        Task processServerMessagesTask = Task.Run(() =>
            ProcessServerMessages(_processServerMessagesCancellationTokenSource.Token));

        Task.WaitAll(readInputTask, processInputTask);

        if (State.err != (State)_currState)
            Interlocked.Exchange(ref _currState, (int)State.end);
        _stateChangedSignal.Set();
        ProcessClientBuffer(tcpSocketManager);


        _processServerMessagesCancellationTokenSource.Cancel();
        _receiveServerMessagesCancellationTokenSource.Cancel();
        Task.WaitAll(receiveServerMessagesTask, processServerMessagesTask);
        tcpSocketManager.Dispose();
        DisposeResources();
        return _returnCode;
    }

    /// <summary>
    /// Process messages received from server 
    /// </summary>
    /// <param name="cancellationToken">Token for stop processing messages</param>
    private Task ProcessServerMessages(CancellationToken cancellationToken)
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

                ProcessServerMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return Task.CompletedTask;
    }
}