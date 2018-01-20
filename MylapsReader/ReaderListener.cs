namespace MylapsReader
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class ReaderListener
    {
        private IPAddress addressValue = IPAddress.Any;
        private int portValue = 1693;
        private Encoding encodingValue = Encoding.Default;

        private TcpListener listener = null;
        private CancellationTokenSource cancellationTokenSource = null;

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
            listener = new TcpListener(Address, Port);
            listener.Start();
            listener.BeginAcceptTcpClient(new AsyncCallback(AcceptTcpClient), new AcceptClientState(listener, cancellationTokenSource.Token));
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
        }

        protected virtual void AcceptTcpClient(IAsyncResult asyncResult)
        {
            var state = (AcceptClientState)asyncResult.AsyncState;

            try
            {
                var client = state.Listener.EndAcceptTcpClient(asyncResult);

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

                if (!state.Token.IsCancellationRequested)
                {
                    ProcessIncomingData(state.Buffer, byteCount, state);

                    state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, new AsyncCallback(Read), state);
                }
                else
                {
                    state.Client.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                // Nothing
            }
        }

        protected virtual void ProcessIncomingData(byte[] buffer, int byteCount, ClientReadState state)
        {
            var chars = new char[1000];
            var charsCount = state.Decoder.GetChars(buffer, 0, byteCount, chars, 0);
            state.StringBuilder.Append(chars, 0, charsCount);

            while (true)
            {
                if (state.Token.IsCancellationRequested)
                {
                    break;
                }

                var idx = -1;
                for (var i = 0; i < state.StringBuilder.Length; i++)
                {
                    if (state.StringBuilder[i] == MessageDelimiter)
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
                    var msg = state.StringBuilder.ToString(0, idx);
                    OnNewMessage(msg, state);
                }

                // remove command and command delimiter
                state.StringBuilder.Remove(0, idx + 1);
            }
        }

        protected virtual void OnNewMessage(string message, ClientReadState state)
        {
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
            var bytes = state.Encoding.GetBytes(text + MessageDelimiter);
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
