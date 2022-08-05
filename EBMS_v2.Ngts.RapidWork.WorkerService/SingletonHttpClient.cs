using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace EBMS_v2.Ngts.RapidWork.WorkerService
{
    public sealed class SingletonHttpClient
    {
        private static readonly SingletonHttpClient instance = new SingletonHttpClient();

        public HttpClient HttpClient = new HttpClient(new HttpClientHandler
        {
            UseDefaultCredentials = true
        });

        static SingletonHttpClient() { }

        private SingletonHttpClient() { }

        public static SingletonHttpClient Instance
        {
            get { return instance; }
        }
    }
}
