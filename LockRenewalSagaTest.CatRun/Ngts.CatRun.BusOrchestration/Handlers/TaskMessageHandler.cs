using Azure.Messaging.ServiceBus;
using NServiceBus;
using NServiceBus.Logging;
using System;
using System.Threading.Tasks;

namespace Ngts.CatRun.BusOrchestration
{
    public class TaskMessageHandler: HandlerBase, IHandleMessages<TaskMessage>
    {
        public TaskMessageHandler(ServiceBusSettings connection) : base(connection) { }

        private static ILog _logger = LogManager.GetLogger<TaskMessageHandler>();

        public async Task Handle(TaskMessage message, IMessageHandlerContext context)
        {
            var nativeMessageId = context.Extensions.Get<ServiceBusReceivedMessage>().MessageId;

            _logger.Info($"{timeStamp(false)} Invoking MessageHandler - completes in {message.TaskDuration.TotalSeconds} seconds at {DateTime.Now.Add(message.TaskDuration).ToString("H:mm:ss tt").ToLower()} for message Id: {nativeMessageId} (nsb id: {context.MessageId})");

            await Task.Delay((int)message.TaskDuration.TotalSeconds * 1000).ConfigureAwait(false);

            // AddTaskProgress(message.JobId, 1, message.TaskId, message);
            _logger.Info($"{timeStamp(true)} After {message.TaskDuration}, TaskId {message.TaskId} MessageHandler COMPLETED");
        }
    }
}
