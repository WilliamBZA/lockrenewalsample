using NServiceBus;
using NServiceBus.Logging;
using System;
using System.Threading.Tasks;

namespace Ngts.CatRun.BusOrchestration
{
    public class AnnualTaxRunHandler : HandlerBase, IHandleMessages<AnnualTaxRun>
    {
        public AnnualTaxRunHandler(ServiceBusSettings connection): base(connection) { }

        private static ILog _logger = LogManager.GetLogger<AnnualTaxRunHandler>();

        public async Task Handle(AnnualTaxRun message, IMessageHandlerContext context)
        {
            const int taskCount = 7;

            var jobId = Guid.NewGuid();
            _logger.Info($"{HandlerBase.timeStamp(false)} Handle(AnnualTaxRun Initiated; TaxRunId: {jobId} - completes at about {DateTime.Now.AddMinutes(taskCount).ToString(HandlerBase.logTimeFormat)}.");

            // setup policy before initiating tasks
            InitiatePolicy(jobId, 1, taskCount);

            var startSaga = new InitiateSaga { JobId = jobId, SagaTaskCount = taskCount };
            await SendMessage(context, startSaga);

            for (int i = 1; i <= taskCount; i++)
            {
                int taskDurationSeconds = 5 * 60 + i * 30;                    
                await SendMessage(context, new TaskMessage
                {
                    JobId = jobId,
                    TaskId = i,
                    TaskDuration = TimeSpan.FromSeconds(taskDurationSeconds)
                }).ConfigureAwait(false);
            }

        }
    }
}
