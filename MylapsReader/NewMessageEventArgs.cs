namespace MylapsReader
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public class NewMessageEventArgs : EventArgs
    {
        public string Message { get; set; }

        public IPEndPoint RemoteEndpoint { get; set; }

        public Func<string, Task> SendResponseAsync { get; set; }
    }
}
