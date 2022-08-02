using NServiceBus;
using System;

namespace Ngts.CatRun.BusOrchestration
{
    public class InitiateSaga : IEvent
    {
        public Guid JobId { get; set; }
        public int SagaTaskCount { get; set; }
    }
}
