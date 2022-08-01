using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

class LockRenewalBehavior : Behavior<ITransportReceiveContext>
{
    public LockRenewalBehavior(TimeSpan renewLockTokenIn)
    {
        this.renewLockTokenIn = renewLockTokenIn;
    }

    public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
    {
        #region native-message-access

        var message = context.Extensions.Get<ServiceBusReceivedMessage>();

        #endregion

        #region get-connection-and-path

        var transportTransaction = context.Extensions.Get<TransportTransaction>();
        var serviceBusClient = transportTransaction.Get<ServiceBusClient>();

        #endregion

        var messageReceiver = serviceBusClient.CreateReceiver("Samples.ASB.SendReply.LockRenewal");

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        Log.Info($"Incoming message ID: {message.MessageId}");

        _ = RenewLockToken(token);

        #region processing-and-cancellation

        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            Log.Info($"Cancelling renewal task for incoming message ID: {message.MessageId}");
            cts.Cancel();
            cts.Dispose();
        }

        #endregion

        #region renewal-background-task

        async Task RenewLockToken(CancellationToken cancellationToken)
        {
            try
            {
                int attempts = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(renewLockTokenIn, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        await messageReceiver.RenewMessageLockAsync(message, cancellationToken).ConfigureAwait(false);
                        attempts = 0;
                        Log.Info($"{message.MessageId}: Lock renewed untill {message.LockedUntil:s}Z.");
                    }
                    catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        //Log.Error($"{message.MessageId}: Lock lost.", e);
                        return;
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        ++attempts;
                        Log.Warn($"{message.MessageId}: Failed to renew lock (#{attempts:N0}), if lock cannot be renewed within {message.LockedUntil:s}Z message will reappear.", e);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected, no need to process
            }
            catch (Exception e)
            {
                Log.Fatal($"{message.MessageId}: RenewLockToken: " + e.Message, e);
            }
        }

        #endregion
    }

    readonly TimeSpan renewLockTokenIn;
    static readonly ILog Log = LogManager.GetLogger<LockRenewalBehavior>();
}