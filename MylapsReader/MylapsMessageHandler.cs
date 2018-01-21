namespace MylapsReader
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class MylapsMessageHandler
    {
        public const char MessagePartDelimiter = '@';

        private readonly ILogger logger;

        public MylapsMessageHandler(ILogger<MylapsMessageHandler> logger)
        {
            this.logger = logger;
        }

        public delegate Task NewTagVisitsHandler(MylapsMessageHandler source, NewTagVisitsEventArgs args);

        public event NewTagVisitsHandler NewTagVisits;

        public async Task HandleMessageAsync(ReaderListener readerListener, NewMessageEventArgs args)
        {
            var parts = args.Message.Split(new[] { MessagePartDelimiter }, StringSplitOptions.RemoveEmptyEntries);

            var name = parts[0];
            var type = parts[1];

            if (type == "Pong")
            {
                var responseText = string.Concat(name, MessagePartDelimiter, "AckPong", MessagePartDelimiter);
                await args.SendResponseAsync(responseText);
                return;
            }

            if (type == "Store")
            {
                // Sample: PK4015806:26:54.958 1 0F  100181705146F
                // Indexes:0123456789 123456789 123456789 12345678
                var reads = parts
                    .Take(parts.Length - 1) // do not take last one
                    .Skip(2) // skip name and type
                    .Select(x => new TagVisit
                    {
                        Tag = x.Substring(0, 7),
                        Time = TimeSpan.Parse(x.Substring(7, 12)),
                        Unknown1 = int.Parse(x.Substring(20, 1)),
                        Unknown2 = int.Parse(x.Substring(22, 2), System.Globalization.NumberStyles.HexNumber),
                        Antenna = x.Substring(26),
                    })
                    .ToArray();

                var msgNumber = long.Parse(parts.Last());

                await OnNewTagVisitsAsync(name, msgNumber, reads);

                var responseText = string.Concat(name, MessagePartDelimiter, "AckStore", MessagePartDelimiter, msgNumber, MessagePartDelimiter);
                await args.SendResponseAsync(responseText);
                return;
            }

            logger.LogError($"Unknown message type '{type}' (ignored)");
        }

        protected virtual async Task OnNewTagVisitsAsync(string name, long sequence, TagVisit[] data)
        {
            var args = new NewTagVisitsEventArgs
            {
                Name = name,
                Sequence = sequence,
                TagVisits = data,
            };

            await NewTagVisits?.Invoke(this, args);
        }
    }
}
