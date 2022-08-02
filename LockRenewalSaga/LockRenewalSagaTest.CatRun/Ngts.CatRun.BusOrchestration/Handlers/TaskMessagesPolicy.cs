using NServiceBus;
using NServiceBus.Logging;
using System;
using System.Threading.Tasks;

namespace Ngts.CatRun.BusOrchestration
{
    public class TaskMessagesPolicy : Saga<TaskMessagesPolicy.TaskMessagesCompleteData>,
        IAmStartedByMessages<InitiateSaga>,
        IHandleTimeouts<TaskMessagesPolicy.TaskMessagePolicyTimeout>
    {
        private static ILog _logger = LogManager.GetLogger<TaskMessagesPolicy>();
        HandlerBase _handlerBase;

        public TaskMessagesPolicy(ServiceBusSettings settings)
        {
            _handlerBase = new HandlerBase(settings);
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TaskMessagesCompleteData> mapper)
        {
            mapper.MapSaga(sagaData => sagaData.JobId)
                .ToMessage<InitiateSaga>(message => message.JobId);
        }

        public async Task Handle(InitiateSaga message, IMessageHandlerContext context)
        {
            Data.JobId = message.JobId;
            await RequestTimeout<TaskMessagePolicyTimeout>(context, TimeSpan.FromMinutes(_handlerBase.SagaIntervalMinutes));
        }

        public async Task Timeout(TaskMessagePolicyTimeout timeout, IMessageHandlerContext context)
        {
            var remainingNonQualifiedCount = _handlerBase.GetRemaingTaskCount(Data.JobId, 1);
            if (remainingNonQualifiedCount == 0)
            {
                _logger.Info($"{HandlerBase.timeStamp(true)} TaskMessagePolicy COMPLETED");
                MarkAsComplete();
            }
            else
            {
                _logger.Info($"{HandlerBase.timeStamp(false)} TaskMessagePolicy INVOKED, {remainingNonQualifiedCount} TaskMessagePolicy tasks have not yet completed.");
                await RequestTimeout<TaskMessagePolicyTimeout>(context, TimeSpan.FromMinutes(_handlerBase.SagaIntervalMinutes));
            }
        }

        public class TaskMessagesCompleteData : ContainSagaData
        {
            public Guid JobId { get; set; }
        }

        public class TaskMessagePolicyTimeout : IEvent { }
    }
}
