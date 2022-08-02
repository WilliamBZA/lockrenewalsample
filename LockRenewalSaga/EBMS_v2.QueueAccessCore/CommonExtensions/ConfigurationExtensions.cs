using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EBMS_v2.QueueAccessCore.CommonExtensions
{
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Helper method to return a strongly typed configuration section based on the generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hostContext"></param>
        /// <returns></returns>
        public static T GetConfig<T>(this HostBuilderContext hostContext)
        {
            var name = typeof(T).Name;
            return hostContext.Configuration.GetSection(name).Get<T>();
        }
    }
}
