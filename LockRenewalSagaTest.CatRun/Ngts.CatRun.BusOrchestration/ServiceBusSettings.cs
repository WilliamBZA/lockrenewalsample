namespace Ngts.CatRun.BusOrchestration
{
    public class ServiceBusSettings
    {
        public string AzureServiceBus { get; set; }
        public string NServiceBusState { get; set; }
        public string MainQueueName { get; set; }
        public int SagaIntervalMinutes { get; set; } = 1;
        public bool BypassNServiceBusSendPublish { get; set; } = false;

        public ServiceBusSettings(string azureServiceBus, string nServiceBuseState, string mainQueueName)
        {
            AzureServiceBus = azureServiceBus;
            NServiceBusState= nServiceBuseState;
            MainQueueName = mainQueueName;
        }
    }
}
