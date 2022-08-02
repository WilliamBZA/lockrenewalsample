using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ngts.CatRun.BusOrchestration;
using NServiceBus;

namespace EBMS_v2.Ngts.RapidWork.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMessageSession _session;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
#if DEBUG
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
#endif
                await Task.Delay(1000 * 180, stoppingToken); //180 second increments for displaying the msg on Console.
            }
        }
    }

    public class SendWorker : BackgroundService
    {
        private IMessageSession _session;
        private static bool alreadySent = false;

        public SendWorker(IMessageSession session)
        {
            _session = session;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!alreadySent)
                await _session.SendLocal(new AnnualTaxRun { });
            alreadySent = true;
        }
    }
}
