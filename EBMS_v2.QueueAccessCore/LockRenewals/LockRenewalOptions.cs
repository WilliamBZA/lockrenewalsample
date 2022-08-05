using System;


namespace EBMS_v2.QueueAccessCore.LockRenewals
{
    public class LockRenewalOptions
    {
        public TimeSpan LockDuration { get; set; }
        public TimeSpan ExecuteRenewalBefore { get; set; }
        public string EndpointName { get; set; }
    }
}