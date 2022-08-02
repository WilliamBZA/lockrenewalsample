using System;
using NServiceBus;

namespace Ngts.CatRun.BusOrchestration
{
    public class TaskMessage: ICommand
    {
        public Guid JobId { get; set; }
        public int TaskId { get; set; }
        public TimeSpan TaskDuration { get; set; }
    }
}
