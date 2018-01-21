namespace MylapsReader
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class ReaderListener
    {
        private readonly ILogger logger;

        private IPAddress addressValue = IPAddress.Any;
        private int portValue = 1693;
        private Encoding encodingValue = Encoding.Default;

        private TcpListener listener = null;
        private CancellationTokenSource cancellationTokenSource = null;

        public ReaderListener(ILogger<ReaderListener> logger)
        {
            this.logger = logger;
        }

        public delegate Task NewMessageHandler(ReaderListener source, NewMessageEventArgs args);

        public event NewMessageHandler NewMessage;

        public bool IsStarted
        {
            get { return listener != null; }
        }

        public IPAddress Address
        {
            get => addressValue;

            set
            {
                if (IsStarted)
                {
                    throw new InvalidOperationException("Can't change Address when started");
                }
                else
                {
                    addressValue = value;
                }
            }
        }

        public int Port
        {
            get => portValue;

            set
            {
                if (IsStarted)
                {
                    throw new InvalidOperationException("Can't change Port when started");
                }
                else
                {
                    portValue = value;
                }
            }
        }

        public char MessageDelimiter { get; set; } = '$';

        public bool TrimNewLines { get; set; } = true;

        public Encoding Encoding
        {
            get => encodingValue;

            set
            {
                if (IsStarted)
                {
                    throw new InvalidOperationException("Can't change Encoding when started");
                }
                else
                {
                    encodingValue = value;
                }
            }
        }

        public void Start()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Already started");
            }

            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }

            cancellationTokenSource = new CancellationTokenSource();

            logger.LogDebug("Starting on port {1} of {0}...", Address, Port);
            listener = new TcpListener(Address, Port);
            listener.Start();
            logger.LogDebug("Started OK, local endpoint is {0}.", listener.LocalEndpoint.ToString());

            listener.BeginAcceptTcpClient(new AsyncCallback(AcceptTcpClient), new AcceptClientState(listener, cancellationTokenSource.Token));
            logger.LogDebug("Waiting for client...");
        }

        public void Stop()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("Not started");
            }

            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }

            listener.Stop();
            listener = null;
            logger.LogDebug("Stopped OK.");
        }

        protected virtual void AcceptTcpClient(IAsyncResult asyncResult)
        {
            var state = (AcceptClientState)asyncResult.AsyncState;

            try
            {
                var client = state.Listener.EndAcceptTcpClient(asyncResult);
                logger.LogDebug("New client connected: {0}", client.Client.RemoteEndPoint);

                if (!state.Token.IsCancellationRequested)
                {
                    state.Listener.BeginAcceptTcpClient(new AsyncCallback(AcceptTcpClient), state);

                    var buffer = new byte[1000];
                    var stream = client.GetStream();
                    var readState = new ClientReadState(client, stream, state.Token, buffer, Encoding);
                    stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(Read), readState);
                }
                else
                {
                    logger.LogDebug("Closing connection from {0} (global cancellation token is set)", client.Client.RemoteEndPoint);
                    client.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                // Nothing
            }
        }

        protected virtual void Read(IAsyncResult asyncResult)
        {
            var state = (ClientReadState)asyncResult.AsyncState;

            try
            {
                var byteCount = state.Stream.EndRead(asyncResult);
                logger.LogDebug("Received {0} bytes from {1}", byteCount, state.Client.Client.RemoteEndPoint);

                if (byteCount == 0)
                {
                    logger.LogWarning("It seems that client is disconnected. Closing.");
                    state.Client.Close();
                }
                else if (!state.Token.IsCancellationRequested)
                {
                    ProcessIncomingData(state.Buffer, byteCount, state);

                    state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, new AsyncCallback(Read), state);
                }
                else
                {
                    logger.LogDebug("Closing connection from {0} (global cancellation token is set)", state.Client.Client.RemoteEndPoint);
                    state.Client.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                // Nothing
            }
            catch (Exception ex)
            {
                logger.LogError(0, ex, "Processing data failed. Closing client.");
                state.Client.Close();
            }
        }

        protected virtual void ProcessIncomingData(byte[] buffer, int byteCount, ClientReadState state)
        {
            var chars = new char[1000];
            var charsCount = state.Decoder.GetChars(buffer, 0, byteCount, chars, 0);
            var sb = state.StringBuilder;

            sb.Append(chars, 0, charsCount);

            while (true)
            {
                if (state.Token.IsCancellationRequested)
                {
                    break;
                }

                var idx = -1;
                for (var i = 0; i < sb.Length; i++)
                {
                    if (sb[i] == MessageDelimiter)
                    {
                        idx = i;
                        break;
                    }
                }

                // if nothing found
                if (idx == -1)
                {
                    break;
                }

                // if not at first position (e.g. command not empty)
                if (idx > 0)
                {
                    var msg = sb.ToString(0, idx);
                    OnNewMessage(msg, state);
                }

                /*
                Now should remove processed data from buffer
                */

                idx++; // command delimiter itself

                if (TrimNewLines)
                {
                    while (sb.Length > idx)
                    {
                        if (sb[idx] == '\r' || sb[idx] == '\n')
                        {
                            idx++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                sb.Remove(0, idx);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    if (sb.Length > 0)
                    {
                        logger.LogDebug("Data still in buffer: {0}", sb.ToString());
                    }
                }
            }
        }

        protected virtual void OnNewMessage(string message, ClientReadState state)
        {
            logger.LogDebug("OnNewMessage(): {0}", message);
            var args = new NewMessageEventArgs
            {
                Message = message,
                RemoteEndpoint = state.Client.Client.RemoteEndPoint as IPEndPoint,
                SendResponseAsync = (txt) => SendAsync(txt, state),
            };

            NewMessage?.Invoke(this, args).GetAwaiter().GetResult();
        }

        protected virtual Task SendAsync(string text, ClientReadState state)
        {
            var message = text + MessageDelimiter;
            logger.LogDebug("SendAsync() + MessageDelimiter: {0}", message);
            var bytes = state.Encoding.GetBytes(message);
            return state.Stream.WriteAsync(bytes, 0, bytes.Length);
        }

#pragma warning disable SA1401 // Fields must be private
        protected class AcceptClientState
        {
            public TcpListener Listener;
            public CancellationToken Token;

            public AcceptClientState(TcpListener listener, CancellationToken token)
            {
                this.Listener = listener;
                this.Token = token;
            }
        }

        protected class ClientReadState
        {
            public TcpClient Client;
            public NetworkStream Stream;
            public CancellationToken Token;
            public byte[] Buffer;
            public Encoding Encoding;
            public Decoder Decoder;

            public ClientReadState(TcpClient client, NetworkStream stream, CancellationToken token, byte[] buffer, Encoding encoding)
            {
                this.Client = client;
                this.Stream = stream;
                this.Token = token;
                this.Buffer = buffer;
                this.Encoding = encoding;
                this.Decoder = encoding.GetDecoder();
            }

            public StringBuilder StringBuilder { get; } = new StringBuilder(1000);
        }
#pragma warning restore SA1401 // Fields must be private
    }
}
