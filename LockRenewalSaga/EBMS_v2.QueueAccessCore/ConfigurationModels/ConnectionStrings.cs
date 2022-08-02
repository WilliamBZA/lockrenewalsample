using System;
using System.Collections.Generic;
using System.Text;

namespace EBMS_v2.QueueAccessCore.ConfigurationModels
{
    public class ConnectionStrings
    {
        public string AzureServiceBus { get; set; }
        public string ApplicationConnectionString { get; set; }
        public string NServiceBusState { get; set; }
    }
}
