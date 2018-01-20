namespace MylapsReader
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ReaderListenerTests
    {
        [Fact]
        public async Task AcceptConnections()
        {
            var msg = "Hello, World!";

            var receivedMsg = string.Empty;
            var evt = new ManualResetEventSlim(false);

            var listener = new ReaderListener();
            listener.NewMessage += (src, args) =>
            {
                receivedMsg = args.Message;
                evt.Set();
                return Task.CompletedTask;
            };
            listener.Start();

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, listener.Port);
                var stream = client.GetStream();

                var bytes = Encoding.UTF8.GetBytes(msg + listener.MessageDelimiter);

                await stream.WriteAsync(bytes, 0, bytes.Length);
            }

            var success = evt.Wait(500);

            listener.Stop();

            Assert.True(success);
            Assert.Equal(msg, receivedMsg);
        }

        [Fact]
        public async Task AcceptSeveralConnections()
        {
            var msg1 = "Hello, World! #1";
            var msg2 = "Hello, World! #2";

            var receivedMessages = new List<string>();

            var evt = new ManualResetEventSlim(false);

            var listener = new ReaderListener();
            listener.NewMessage += (src, args) =>
            {
                receivedMessages.Add(args.Message);
                if (receivedMessages.Count == 2)
                {
                    evt.Set();
                }

                return Task.CompletedTask;
            };
            listener.Start();

            using (var client1 = new TcpClient())
            {
                await client1.ConnectAsync(IPAddress.Loopback, listener.Port);
                var stream1 = client1.GetStream();
                var bytes1 = Encoding.UTF8.GetBytes(msg1 + listener.MessageDelimiter);

                using (var client2 = new TcpClient())
                {
                    await client2.ConnectAsync(IPAddress.Loopback, listener.Port);
                    var stream2 = client2.GetStream();
                    var bytes2 = Encoding.UTF8.GetBytes(msg2 + listener.MessageDelimiter);

                    await stream2.WriteAsync(bytes2, 0, bytes2.Length);
                    await stream1.WriteAsync(bytes1, 0, bytes1.Length);
                }
            }

            var success = evt.Wait(500);

            listener.Stop();

            Assert.True(success);
            Assert.Contains(msg1, receivedMessages);
            Assert.Contains(msg2, receivedMessages);
        }

        [Fact]
        public async Task ReceiveSeveralMessages()
        {
            var msg1 = "Hello, World! #1";
            var msg2 = "Hello, World! #2";

            var receivedMessages = new List<string>();

            var evt = new ManualResetEventSlim(false);

            var listener = new ReaderListener();
            listener.NewMessage += (src, args) =>
            {
                receivedMessages.Add(args.Message);
                if (receivedMessages.Count == 2)
                {
                    evt.Set();
                }

                return Task.CompletedTask;
            };
            listener.Start();

            using (var client1 = new TcpClient())
            {
                await client1.ConnectAsync(IPAddress.Loopback, listener.Port);
                var stream = client1.GetStream();

                var bytes1 = Encoding.UTF8.GetBytes(msg1 + listener.MessageDelimiter);
                await stream.WriteAsync(bytes1, 0, bytes1.Length);

                var bytes2 = Encoding.UTF8.GetBytes(msg2 + listener.MessageDelimiter);
                await stream.WriteAsync(bytes2, 0, bytes2.Length);
            }

            var success = evt.Wait(500);

            listener.Stop();

            Assert.True(success);
            Assert.Contains(msg1, receivedMessages);
            Assert.Contains(msg2, receivedMessages);
        }
    }
}
