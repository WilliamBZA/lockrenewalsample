using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus.Administration;
using LockRenewal;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Samples.ASB.LockRenewal";

        var endpointConfiguration = new EndpointConfiguration("Samples.ASB.SendReply.LockRenewal");
        endpointConfiguration.EnableInstallers();

        var connectionString = "";
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("Could not read the 'AzureServiceBus_ConnectionString' environment variable. Check the sample prerequisites.");
        }

        var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>().ConnectionString(connectionString);

        //transport.PrefetchCount(0);
        //transport.PrefetchMultiplier(1);

        //endpointConfiguration.LimitMessageProcessingConcurrencyTo(1);

        #region override-lock-renewal-configuration

        endpointConfiguration.LockRenewal(options =>
        {
            options.LockDuration = TimeSpan.FromSeconds(30);
            options.ExecuteRenewalBefore = TimeSpan.FromSeconds(5);
        });

        endpointConfiguration.Recoverability().Immediate(a => a.NumberOfRetries(0));
        endpointConfiguration.Recoverability().Delayed(a => a.NumberOfRetries(0));

        

        #endregion

        ConfigureTransactionTimeoutCore(TimeSpan.FromMinutes(45));

        var endpointInstance = await Endpoint.Start(endpointConfiguration).ConfigureAwait(false);

        await OverrideQueueLockDuration("Samples.ASB.SendReply.LockRenewal", connectionString).ConfigureAwait(false);

        for (int i = 1; i <= 21; i++)
        {
            int taskDurationSeconds = i % 4 == 0 ? 180 : 5 * 60 + i * 30;
            await endpointInstance.SendLocal(new LongProcessingMessage { ProcessingDuration = TimeSpan.FromSeconds(taskDurationSeconds) });  
        }

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();

        await endpointInstance.Stop().ConfigureAwait(false);
    }

    private static async Task OverrideQueueLockDuration(string queuePath, string connectionString)
    {
        var managementClient = new ServiceBusAdministrationClient(connectionString);
        var queueDescription = await managementClient.GetQueueAsync(queuePath).ConfigureAwait(false);
        queueDescription.Value.LockDuration = TimeSpan.FromSeconds(30);

        await managementClient.UpdateQueueAsync(queueDescription.Value).ConfigureAwait(false);
    }

    #region override-transaction-manager-timeout-net-core

    static void ConfigureTransactionTimeoutCore(TimeSpan timeout)
    {
        SetTransactionManagerField("s_cachedMaxTimeout", true);
        SetTransactionManagerField("s_maximumTimeout", timeout);

        void SetTransactionManagerField(string fieldName, object value) =>
            typeof(TransactionManager)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
    }

    #endregion

    #region override-transaction-manager-timeout-net-framework

    static void ConfigureTransactionTimeoutNetFramework(TimeSpan timeout)
    {
        SetTransactionManagerField("_cachedMaxTimeout", true);
        SetTransactionManagerField("_maximumTimeout", timeout);

        void SetTransactionManagerField(string fieldName, object value) =>
            typeof(TransactionManager)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
    }

    #endregion
}