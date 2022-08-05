using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

namespace EBMS_v2.QueueAccessCore.LockRenewals
{

    public class LockRenewalBehavior : Behavior<ITransportReceiveContext>
    {
        readonly TimeSpan lockDuration;
        readonly TimeSpan renewLockTokenIn;
        readonly string queueName;

        private static readonly ILog Log = LogManager.GetLogger<LockRenewalBehavior>();

        public LockRenewalBehavior(TimeSpan lockDuration, TimeSpan renewLockTokenIn, string queueName)
        {
            this.lockDuration = lockDuration;
            this.renewLockTokenIn = TimeSpan.FromSeconds(25);
            this.queueName = queueName;
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

            var messageReceiver = serviceBusClient.CreateReceiver(queueName);
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            Log.Info($"Incoming message ID: {message.MessageId}");

            _ = RenewLockToken(token);

            #region processing-and-cancellation

            try
            {
                await next().ConfigureAwait(false);
            }
            catch(MessageDeserializationException ex)
            {
                Log.Error("Could not deserialize message JSON string", ex);
                throw;
            }
            finally
            {
                Log.Info($"Cancelling renewal task for incoming message ID: {message.MessageId}");

                //var remaining = message.LockedUntil - DateTimeOffset.UtcNow;
                //if (remaining < renewLockTokenIn)
                //    Log.Warn($"{message.MessageId}: Processing completed but LockedUntil {message.LockedUntil:s}Z less than {renewLockTokenIn}. This could indicate issues during lock renewal.");

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
                        Log.Info($"Lock for message {message.MessageId} will be renewed in {renewLockTokenIn}");

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
                catch (OperationCanceledException)
                {
                    Log.Info($"Lock renewal task for incoming message ID: {message.MessageId} was cancelled.");
                }
                catch (Exception exception)
                {
                    Log.Error($"Failed to renew lock for incoming message ID: {message.MessageId}", exception);
                }
            }

            #endregion
        }
    }
}