namespace MylapsReader
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using Microsoft.Extensions.Logging;

    public static class Program
    {
        public static void Main(string[] args)
        {
            var loggerFactory = new LoggerFactory()
                .AddConsole(LogLevel.Trace)
                .AddFile("log.txt", LogLevel.Trace);

            var logger = loggerFactory.CreateLogger(typeof(Program));

            logger.LogInformation("Запуск слушателя...");

            var handler = new MylapsMessageHandler(loggerFactory.CreateLogger<MylapsMessageHandler>());
            handler.NewTagVisits += async (src, a) =>
            {
                logger.LogDebug($"{a.Name} #{a.Sequence}:");
                foreach (var item in a.TagVisits)
                {
                    logger.LogDebug($"  {item.Tag}  {item.Time}:");
                }
            };

            var listener = new ReaderListener(loggerFactory.CreateLogger<ReaderListener>());
            listener.NewMessage += handler.HandleMessageAsync;
            listener.Start();

            logger.LogInformation("Всё успешно запущено!");

            var ips = GetLocalIPs();
            logger.LogInformation(
                "Настройте ридер на отправку данных на порт {0} на один из адресов: {1}",
                listener.Port,
                string.Join(" или ", ips));

            Console.WriteLine();
            Console.WriteLine("Нажмите ENTER для завершения");
            Console.WriteLine();
            Console.ReadLine();

            logger.LogDebug("Остановка слушателя...");
            listener.Stop();

            Console.WriteLine();
            Console.WriteLine("Всё остановлено. Можно просто закрыть данное окно.");
            Console.WriteLine();
        }

        private static List<IPAddress> GetLocalIPs()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up)
                .Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet || x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(x => x.Address)
                .ToList();
        }
    }
}
