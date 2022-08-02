using System;
using System.Collections.Generic;
using System.Text;

namespace EBMS_v2.QueueAccessCore.ConfigurationModels
{
    public class NServiceBusConfig
    {
        public string TopicName { get; set; } = "bundle-1"; //For whatever reason, this is the NServiceBus default Topic name.
        public string MainQueueName { get; set; }
        public string ErrorQueue { get; set; } = "error";
        public string AuditQueue { get; set; } = "audit";
        public string ServiceControlQueue { get; set; } = "EBMS_v2.Logging";
        public string ServiceControlMetricsQueue { get; set; } = "EBMS_v2.Monitoring";
        public int MaxMessageProcessingConcurrency { get; set; } = 0; // 0 == use default of processor count;
        public bool EnableInstallers { get; set; } = true;
        public string SqlPersistentSchema { get; set; } = "nsb";
        public int ImmediateRetryCount { get; set; } = 0;
        public int DelayedRetryCount { get; set; } = 0;
        public int PrefetchCount { get; set; } = 0;
        public int PrefetchMultiplier { get; set; } = 0;
        public bool EnablePartitioning { get; set; } = true;
        public int EntityMaximumSize { get; set; } = 5;
        public int LimitMessageProcessingConcurrencyTo { get; set; } = 0;
        public int DelayedTimeIncreaseSeconds { get; set; } = 10;

        public int MessageLockTimeSpanSeconds { get; set; } = 30;

        public int TotalTransactionTimeSpanMins { get; set; } = 30;
    }
}
