using EBMS_v2.QueueAccessCore.CommonExtensions;
using EBMS_v2.QueueAccessCore.ConfigurationModels;
using EBMS_v2.QueueAccessCore.LockRenewals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ngts.CatRun.BusOrchestration;
using NLog.Config;
using NLog.Extensions.Logging;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Extensions.Logging;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Transactions;

namespace EBMS_v2.Ngts.RapidWork.WorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    string exeFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\";
                    configApp.AddJsonFile($"{exeFolderPath}appSettings.json", optional: false, reloadOnChange: true)
                             .AddJsonFile($"{exeFolderPath}appSettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureLogging((hostContext, logBuilder) =>
                {
                    var instrumetationKey = hostContext.Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey");
                    logBuilder.AddApplicationInsights(instrumetationKey);
                    ConfigureLoggingWithNLog(hostContext, logBuilder);
                })
                //https://dotnetcoretutorials.com/2019/12/07/creating-windows-services-in-net-core-part-3-the-net-core-worker-way/
                //This is ONLY used for on-prem Windows Service deployment... 
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    var serviceBusSettings = new ServiceBusSettings(
                        hostContext.GetConfig<ConnectionStrings>().AzureServiceBus,
                        hostContext.GetConfig<ConnectionStrings>().NServiceBusState,
                        hostContext.GetConfig<NServiceBusConfig>().MainQueueName
                    );
                    services.AddSingleton(typeof(ServiceBusSettings), serviceBusSettings);

                    services.AddHostedService<Worker>();
                    HandlerBase.InitializeSql(hostContext.GetConfig<ConnectionStrings>().NServiceBusState);
                })
                .UseNServiceBus(hostContext =>
                {
                    EndpointConfiguration endpointConfiguration = SetupNServiceBusEndpoints(hostContext);
                    return endpointConfiguration;
                });

        static Action<string, object> SetTransactionManagerField = (fieldName, value) =>
            typeof(TransactionManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

        private static EndpointConfiguration SetupNServiceBusEndpoints(HostBuilderContext hostContext)
        {
            var nServiceBusConfig = hostContext.GetConfig<NServiceBusConfig>();
            var connectionsConfig = hostContext.GetConfig<ConnectionStrings>();

            var endpointConfiguration = new EndpointConfiguration(nServiceBusConfig.MainQueueName);
            endpointConfiguration.SendFailedMessagesTo(nServiceBusConfig.ErrorQueue);

            // Configure audit queue inside NSB. Standard setup... to support Service-Insights.
            if (!string.IsNullOrWhiteSpace(nServiceBusConfig.AuditQueue))
                endpointConfiguration.AuditProcessedMessagesTo(nServiceBusConfig.AuditQueue);

            endpointConfiguration.LimitMessageProcessingConcurrencyTo(nServiceBusConfig.MaxMessageProcessingConcurrency);

            // https://docs.particular.net/samples/azure-service-bus-netstandard/lock-renewal/#overriding-the-value-of-transactionmanager-maxtimeout
            // extend the AzureServiceBus timeout beyond the 10 minute default
            // Note: the transaction timeout timespan needs to be long enought to
            //       allow for the longest running message handler to complete
            SetTransactionManagerField("s_cachedMaxTimeout", true);
            SetTransactionManagerField("s_maximumTimeout", TimeSpan.FromMinutes(nServiceBusConfig.TotalTransactionTimeSpanMins));

            //CWS 03/24/21 - Let NServiceBus create these queues for us...
            if (nServiceBusConfig.EnableInstallers)
                endpointConfiguration.EnableInstallers();

            endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();

            var transport = endpointConfiguration.UseTransport<AzureServiceBusTransport>();
            transport.TopicName(nServiceBusConfig.TopicName);

            var connectionString = connectionsConfig.AzureServiceBus;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new Exception("Could not read the 'AzureServiceBus_ConnectionString' environment variable.");
            }

            //CWS 03/24/21 - Make the Queue max-size configurable.
            transport.ConnectionString(connectionString);
            transport.EntityMaximumSize(nServiceBusConfig.EntityMaximumSize);

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>()
                .Schema(nServiceBusConfig.SqlPersistentSchema);

            persistence.ConnectionBuilder(() => new SqlConnection(connectionsConfig.NServiceBusState));
            var subscriptions = persistence.SubscriptionSettings();
            subscriptions.CacheFor(TimeSpan.FromMinutes(1));

            //CWS 03/12/21 - IMPORTANT---
            //Crucial settings for extremely long-running transactions that are occuring on Azure Service Bus.
            //These settings ensure that the NServiceBus Transaction-Manager doesn't time out on you, 
            //that the lock assigned to your queue message doesn't get released while you're still processing it,
            //and that the default settings of such things on the queue itself, are what you expect. 
            NServiceBus.Settings.SettingsHolder settings = endpointConfiguration.GetSettings();
            settings.Set<LockRenewalOptions>(
                new LockRenewalOptions()
                {
                    LockDuration = TimeSpan.FromSeconds(nServiceBusConfig.MessageLockTimeSpanSeconds),
                    ExecuteRenewalBefore = TimeSpan.FromSeconds(30),
                    EndpointName = nServiceBusConfig.MainQueueName
                });

            var routing = transport.Routing();
            routing.RouteToEndpoint(typeof(AnnualTaxRun).Assembly, nServiceBusConfig.MainQueueName);

            RecoverabilitySettings recoverStgs = endpointConfiguration.Recoverability();
            recoverStgs.Immediate((retrySettings) => retrySettings.NumberOfRetries(nServiceBusConfig.ImmediateRetryCount));
            recoverStgs.Delayed((retrySettings) => retrySettings.NumberOfRetries(nServiceBusConfig.DelayedRetryCount));

            //CWS 03/24/21 - Setup a policy where the message that takes 2 hours to process does NOT automatically 
            //retry if it fails... and allow the other normal messages to retry since their failure is faster and 
            //less impactful.
            //recoverStgs.CustomPolicy((rConfig, errContext) =>
            //{
            //    string enclosedMsgType = errContext.Message.Headers["NServiceBus.EnclosedMessageTypes"];
            //    if (enclosedMsgType.Contains(typeof(DdbiMessage).Name))
            //        return RecoverabilityAction.MoveToError(nServiceBusConfig.ErrorQueue);
            //    else
            //        return DefaultRecoverabilityPolicy.Invoke(rConfig, errContext);
            //});

            return endpointConfiguration;
        }



        private static void ConfigureLoggingWithNLog(HostBuilderContext hostContext, ILoggingBuilder logBuilder)
        {
            var config = new LoggingConfiguration();

            var consoleTarget = new NLog.Targets.ColoredConsoleTarget
            {
                Layout = "${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}"
            };
            var textTarget = new NLog.Targets.FileTarget()
            {
                Layout = consoleTarget.Layout
            };
            config.AddTarget("console", consoleTarget);
            config.AddTarget("file", textTarget);

            config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Debug, consoleTarget));

            NLog.LogManager.Configuration = config;

            Microsoft.Extensions.Logging.ILoggerFactory extensionsLoggerFactory = new NLogLoggerFactory();
            NServiceBus.Logging.ILoggerFactory nservicebusLoggerFactory = new ExtensionsLoggerFactory(loggerFactory: extensionsLoggerFactory);
            NServiceBus.Logging.LogManager.UseFactory(loggerFactory: nservicebusLoggerFactory);
        }
    }
}
