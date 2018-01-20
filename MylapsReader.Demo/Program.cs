namespace MylapsReader
{
    using System;
    using Microsoft.Extensions.Logging;

    public static class Program
    {
        public static void Main(string[] args)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);

            var logger = loggerFactory.CreateLogger(typeof(Program));

            logger.LogInformation("Hello World!");

            var handler = new MylapsMessageHandler(loggerFactory.CreateLogger<MylapsMessageHandler>());
            handler.NewTagVisits += async (src, a) =>
            {
                logger.LogDebug($"{a.Name} #{a.Sequence}:");
                foreach (var item in a.TagVisits)
                {
                    logger.LogDebug($"  {item.Tag}  {item.Time}:");
                }
            };

            var listener = new ReaderListener();
            listener.NewMessage += handler.HandleMessageAsync;
            listener.Start();

            Console.WriteLine();
            Console.WriteLine("Press RETURN to quit");
            Console.WriteLine();
            Console.ReadLine();
        }
    }
}
