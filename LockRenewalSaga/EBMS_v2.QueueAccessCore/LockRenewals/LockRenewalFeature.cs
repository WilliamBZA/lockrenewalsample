using NServiceBus.Features;

namespace EBMS_v2.QueueAccessCore.LockRenewals
{
    public class LockRenewalFeature : Feature
    {
        internal LockRenewalFeature()
        {
            #region LockRenewalFeature

            EnableByDefault();

            //Defaults(settings =>
            //{
            //    settings.SetDefault<LockRenewalOptions>(new LockRenewalOptions
            //    {
            //        // NServiceBus.Transport.AzureServiceBus sets LockDuration to 5 minutes by default
            //        LockDuration = TimeSpan.FromMinutes(5),
            //        ExecuteRenewalBefore = TimeSpan.FromMinutes(1)
            //    });
            //});

            #endregion
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var lockRenewalOptions = context.Settings.Get<LockRenewalOptions>();

            context.Pipeline.Register(
                stepId: "LockRenewal",
                factoryMethod: builder => new LockRenewalBehavior(
                        lockRenewalOptions.LockDuration,
                        lockRenewalOptions.LockDuration - lockRenewalOptions.ExecuteRenewalBefore,
                        lockRenewalOptions.EndpointName
                    ),
                description: "Renew message lock token"
            );
        }
    }
}