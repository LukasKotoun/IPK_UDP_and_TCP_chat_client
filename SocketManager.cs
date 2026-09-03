using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;
using Timer = System.Timers.Timer;

namespace ipk24chat_client;

public interface ISocketManager<TMessageDataType>
{
    /// <summary>
    /// Receives server messages and adds them to the specified collection
    /// In tcp first split that messages by tcp message termination character
    /// </summary>
    /// <param name="collection">The collection for adding received messages</param>
    /// <param name="handleSocketError">Action to handle socket error</param>
    /// <param name="cancellationToken">Cancellation token to stop receiving messages</param>
    public Task ReceiveServerMessages(BlockingCollection<TMessageDataType> collection, Action handleSocketError,
        CancellationToken cancellationToken);

    public void Dispose();
}

/// <summary>
/// Class for socket management with socket client and message data type
/// </summary>
/// <typeparam name="TSocketClient">The type of socket client</typeparam>
/// <typeparam name="TMessageDataType">The type of message data</typeparam>
public abstract class SocketManager<TSocketClient, TMessageDataType> : ISocketManager<TMessageDataType>
{
    protected string ServerHostName;
    protected ushort ServerPortNumber;
    protected TSocketClient SocketClient;

    protected SocketManager(string serverHostName, ushort serverPortNumber)
    {
        ServerHostName = serverHostName;
        ServerPortNumber = serverPortNumber;
    }

    /// <inheritdoc />
    public abstract Task ReceiveServerMessages(BlockingCollection<TMessageDataType> collection,
        Action handleSocketError,
        CancellationToken cancellationToken);

    public abstract void SendMessage(TMessageDataType message, ushort messageId);


    public abstract void Dispose();
}

/// <summary>
/// Implementation for UDP communication
/// </summary>
public class UdpSocketMannager : SocketManager<UdpClient, byte[]>
{
    private ushort _confirmTimeOut;
    private byte _maxRetransmissions;
    private AddressFamily _listeningIpAddressFamily;
    private ushort _listeningPort;
    private AutoResetEvent _confirmedMessagesChangedSignal;
    RefWrapper<ushort?> _lastConfirmedMessageId;
    private bool _usingServerDynPort;

    public UdpSocketMannager(string serverHostName, ushort serverPortNumber,
        ushort confirmTimeOut, byte maxRetransmissions, AutoResetEvent confirmedMessagesChangedSignal,
        RefWrapper<ushort?> lastConfirmedMessageId,
        AddressFamily listeningIpAddressFamily = AddressFamily.InterNetwork, ushort listeningPort = 0) : base(
        serverHostName, serverPortNumber)
    {
        ServerHostName = serverHostName;
        ServerPortNumber = serverPortNumber;
        _confirmTimeOut = confirmTimeOut;
        _maxRetransmissions = maxRetransmissions;
        _listeningIpAddressFamily = listeningIpAddressFamily;
        _listeningPort = listeningPort;
        _confirmedMessagesChangedSignal = confirmedMessagesChangedSignal;
        _lastConfirmedMessageId = lastConfirmedMessageId;
        _usingServerDynPort = false;
        try
        {
            SocketClient = new(listeningPort, listeningIpAddressFamily);
        }
        catch (Exception)
        {
            throw new SocketException();
        }
    }

    /// <summary>
    /// Sends messages using socket manager
    /// While message confirmation is not received in corresponding time retry sending
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="messageId">Message id to check confirmation</param>
    /// <exception cref="SocketException">If there was error while using socket</exception>
    /// <exception cref="TimeoutException">If confirmation was not received after exact number of retransmissions</exception>
    public override void SendMessage(byte[] message, ushort messageId)
    {
        if (message.Length == 0)
            return;
        AutoResetEvent timerStoppedEvent = new(false);
        
        //first sending
        try
        {
            SocketClient.Send(message, message.Length, ServerHostName, ServerPortNumber);
        }
        catch (Exception)
        {
            throw new SocketException();
        }
        
        // potential retransmissions 
        byte retransmissionCount = 0;
        Timer timer = new Timer(_confirmTimeOut);
        timer.Elapsed += (sender, e) =>
        {
            if (retransmissionCount >= _maxRetransmissions)
            {
                timer.Stop();
                timerStoppedEvent.Set();
            }
            else
            {
                try
                {
                    SocketClient.Send(message, message.Length, ServerHostName, ServerPortNumber);
                }
                catch (Exception)
                {
                    throw new SocketException();
                }
                retransmissionCount++;
            }
        };
        timer.AutoReset = true;
        timer.Start();
        
        // Checking for message confirmation
        while (_lastConfirmedMessageId.Value == null || _lastConfirmedMessageId.Value < messageId)
        {
            int eventNumber =
                WaitHandle.WaitAny(new WaitHandle[] { _confirmedMessagesChangedSignal, timerStoppedEvent });
            if (eventNumber == 1)
            {
                if (_lastConfirmedMessageId.Value >= messageId)
                    break;
                timer.Stop();
                timer.Dispose();
                throw new TimeoutException();
            }
        }

        timer.Stop();
        timer.Dispose();
    }

    public void SendOneTimeMessage(byte[] message)
    {
        try
        {
            SocketClient.Send(message, message.Length, ServerHostName, ServerPortNumber);
        }
        catch (Exception)
        {
            throw new SocketException();
        }
    }

    /// <inheritdoc />
    public override async Task ReceiveServerMessages(BlockingCollection<byte[]> collection,
        Action handleSocketError, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await SocketClient.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                handleSocketError();
                break;
            }

            //change port to dyn port but only once 
            if (!_usingServerDynPort && ServerPortNumber != (ushort)result.RemoteEndPoint.Port)
            {
                ServerPortNumber = (ushort)result.RemoteEndPoint.Port;
                _usingServerDynPort = true;
            }

            collection.Add(result.Buffer);
        }

        collection.CompleteAdding();
    }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            SocketClient?.Close();
        }
    }
}

/// <summary>
/// Implementation for UDP communication
/// </summary>
public class TcpSocketMannager : SocketManager<TcpClient, string>
{
    private NetworkStream _socketStream;

    //for detecting message end
    private string _tcpMessageTermination;

    public TcpSocketMannager(string serverHostName, ushort serverPortNumber,
        AddressFamily ipAddressFamily = AddressFamily.InterNetwork, string tcpMessageTermination = "\r\n") : base(
        serverHostName, serverPortNumber)
    {
        ServerHostName = serverHostName;
        ServerPortNumber = serverPortNumber;
        SocketClient = new(ipAddressFamily);
        _tcpMessageTermination = tcpMessageTermination;
        try
        {
            SocketClient.Connect(serverHostName, serverPortNumber);
            _socketStream = SocketClient.GetStream();
        }
        catch (Exception)
        {
            throw new SocketException();
        }
    }

    /// <summary>
    /// Sends messages using socket manager
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="messageId">Message id is not used here => only for leaving same interface with udp</param>
    /// <exception cref="SocketException">If there was error while using socket</exception>
    public override void SendMessage(string message, ushort messageId)
    {
        if (message.Length == 0)
            return;
        try
        {
            byte[] buffer = Encoding.ASCII.GetBytes(message + _tcpMessageTermination);
            _socketStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception)
        {
            throw new SocketException();
        }
    }

    /// <inheritdoc />
    public override async Task ReceiveServerMessages(BlockingCollection<string> collection, Action handleSocketError,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1510];
        StringBuilder receivedMessage = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            string data;
            int bytesRead;
            try
            {
                bytesRead = await _socketStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                handleSocketError();
                break;
            }

            if (bytesRead <= 0)
            {
                handleSocketError();
                break;
            }

            //split received data by tcp termination character 
            receivedMessage.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            // Check if the received data contains termination characters and get index of it
            string receivedData = receivedMessage.ToString();
            int lastTerminationIndex = receivedData.LastIndexOf(_tcpMessageTermination, StringComparison.Ordinal);
            if (lastTerminationIndex != -1)
            {
                //work only with data to termination char
                string[] messages = receivedData.Substring(0, lastTerminationIndex).Split(_tcpMessageTermination);
                foreach (string message in messages)
                {
                    collection.Add(message);
                }

                // Remove processed (added to collection) messages 
                receivedMessage.Remove(0, lastTerminationIndex + _tcpMessageTermination.Length);
            }
        }

        collection.CompleteAdding();
    }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _socketStream.Close();
            SocketClient?.Close();
        }
    }
}