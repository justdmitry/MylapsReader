namespace MylapsReader
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class MylapsMessageHandlerTests
    {
        [Fact]
        public async Task HandlePong()
        {
            var loggerMock = new Mock<ILogger<MylapsMessageHandler>>();

            var responses = new List<string>();

            var handler = new MylapsMessageHandler(loggerMock.Object);

            var args = new NewMessageEventArgs
            {
                Message = "Hello@Pong@",
                RemoteEndpoint = null,
                SendResponseAsync = async (x) => responses.Add(x),
            };

            await handler.HandleMessageAsync(null, args);

            Assert.Contains("Hello@AckPong@", responses);
        }

        [Fact]
        public async Task HandleStore()
        {
            var storeMessage = "Hello@Store@KX5183106:07:57.054 1 0F  1000017051463" +
                "@KS2204006:27:38.971 1 0F  3001B17051472@KR9743906:28:44.867 1 0F  1001C1705148A" +
                "@PL1879106:29:02.680 1 0F  1001D17051478@2@";

            var loggerMock = new Mock<ILogger<MylapsMessageHandler>>();

            var responses = new List<string>();
            var args = new List<NewTagVisitsEventArgs>();

            var handler = new MylapsMessageHandler(loggerMock.Object);
            handler.NewTagVisits += async (src, ea) => args.Add(ea);

            var a = new NewMessageEventArgs
            {
                Message = storeMessage,
                RemoteEndpoint = null,
                SendResponseAsync = async (x) => responses.Add(x),
            };

            await handler.HandleMessageAsync(null, a);

            Assert.Contains("Hello@AckStore@2@", responses);
            Assert.Single(args);
            Assert.Equal("Hello", args[0].Name);
            Assert.Equal(2, args[0].Sequence);
            Assert.Equal(4, args[0].TagVisits.Length);

            Assert.Equal("KX51831", args[0].TagVisits[0].Tag);
            Assert.Equal(TimeSpan.Parse("06:07:57.054"), args[0].TagVisits[0].Time);

            Assert.Equal("KS22040", args[0].TagVisits[1].Tag);
            Assert.Equal(TimeSpan.Parse("06:27:38.971"), args[0].TagVisits[1].Time);

            Assert.Equal("KR97439", args[0].TagVisits[2].Tag);
            Assert.Equal(TimeSpan.Parse("06:28:44.867"), args[0].TagVisits[2].Time);

            Assert.Equal("PL18791", args[0].TagVisits[3].Tag);
            Assert.Equal(TimeSpan.Parse("06:29:02.680"), args[0].TagVisits[3].Time);
        }
    }
}
