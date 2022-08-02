using Newtonsoft.Json;
using NServiceBus;
using NServiceBus.Logging;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Ngts.CatRun.BusOrchestration
{
    public class HandlerBase
    {
        public const string logTimeFormat = "MM/dd/yy H:mm:ss";
        public static Func<bool, string> timeStamp = (isComplete) => $"{DateTime.Now.ToString(logTimeFormat)}{(isComplete ? "     >>>>>" : "")}";

        private static ILog _logger = LogManager.GetLogger<HandlerBase>();

        protected string _azureServiceBus;
        protected string _nServiceBusState;
        protected string _MainQueueName;
        public int SagaIntervalMinutes;
        protected bool _bypassNServiceBusSendPublish = false;

        public HandlerBase(ServiceBusSettings settings)
        {
            _azureServiceBus = settings.AzureServiceBus;
            _nServiceBusState = settings.NServiceBusState;
            _MainQueueName = settings.MainQueueName;
            SagaIntervalMinutes = settings.SagaIntervalMinutes;
        }

        public async Task SendMessage<T>(IMessageHandlerContext context, T obj)
        {
            var intent = obj is IEvent
                ? "Publish"
                : "Send";

            _logger.Info($"{DateTime.Now.ToString(logTimeFormat)} context.{intent} {typeof(T).Name}");
            if (obj is IEvent)
                await context.Publish(obj).ConfigureAwait(false);
            else
                await context.Send(obj).ConfigureAwait(false);
        }

        public static void InitializeSql(string connectionString)
        {
            string exeFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\";
            ExecScripts(connectionString, new[] {

                // first, drop tables
                "if object_id('nsb.EbmsPolicyData', 'U') is not null drop table nsb.EbmsPolicyData",
                "if object_id('nsb.ngts_catrun_test_TaskMessagesPolicy', 'U') is not null drop table nsb.ngts_catrun_test_TaskMessagesPolicy",

                File.ReadAllText(Path.Combine(exeFolderPath, @"SqlScripts\CreateSchemaNsb.sql")),
                File.ReadAllText(Path.Combine(exeFolderPath, @"SqlScripts\CreateTableEbmsPolicyData.sql")),
                File.ReadAllText(Path.Combine(exeFolderPath, @"SqlScripts\ebmsClearEventTasks.sql")),
                File.ReadAllText(Path.Combine(exeFolderPath, @"SqlScripts\ebmsGetRemaingTaskCount.sql")),
                File.ReadAllText(Path.Combine(exeFolderPath, @"SqlScripts\ebmsInsertTaskProgress.sql"))
            });
        }

        private static void ExecScripts(string connectionString, string[] scripts)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                sqlConnection.Open();
                foreach (var script in scripts)
                {
                    using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
                    {
                        sqlCommand.CommandText = script;
                        sqlCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        const string InitiateJson = "{ \"type\": \"initializing record\" }";

        public void InitiatePolicy(Guid jobId, int eventId, int totalRecordCount)
        {
            AddTaskProgress(jobId, eventId, null, totalRecordCount, InitiateJson);
        }

        public void AddTaskProgress<T>(Guid jobId, int eventId, int taskId, T obj)
        {
            AddTaskProgress(jobId, eventId, taskId, null, JsonConvert.SerializeObject(obj));
        }

        public void AddTaskProgress(Guid jobId, int eventId, int taskId, string json = null)
        {
            AddTaskProgress(jobId, eventId, taskId, null, json);
        }

        private void AddTaskProgress(Guid jobId, int sagaEventId, int? taskId, int? totalRecordCount, string json = null)
        {
            //using (var sqlConnection = new SqlConnection(_nServiceBusState))
            //{
            //    sqlConnection.Open();
            //    using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
            //    {
            //        sqlCommand.CommandText = "nsb.ebmsInsertTaskProgress";
            //        sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
            //        sqlCommand.Parameters.Add(new SqlParameter("@JobId", jobId));
            //        sqlCommand.Parameters.Add(new SqlParameter("@EventId", sagaEventId));
            //        sqlCommand.Parameters.Add(new SqlParameter("@TaskId", taskId));
            //        if (totalRecordCount.HasValue)
            //            sqlCommand.Parameters.Add(new SqlParameter("@TotalTaskCount", totalRecordCount));
            //        if (json != null)
            //           sqlCommand.Parameters.Add(new SqlParameter("@Json", json));
            //        sqlCommand.ExecuteNonQuery();
            //    }
            //}
        }

        public int? GetRemaingTaskCount(Guid jobId,int eventId)
        {
            using (var sqlConnection = new SqlConnection(_nServiceBusState))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText = "nsb.ebmsGetRemaingTaskCount";
                    sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    sqlCommand.Parameters.Add(new SqlParameter("@JobId", jobId));
                    sqlCommand.Parameters.Add(new SqlParameter("@EventId", eventId));
                    var result = new SqlParameter("@RemainingCount", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                    sqlCommand.Parameters.Add(result);
                    sqlCommand.ExecuteNonQuery();

                    var remainingCount = (int?)result.Value;

                    if (!remainingCount.HasValue || remainingCount < 0)
                        throw new Exception("Policy WAS NOT PROPERLY INITIATED");

                    return (int?)result.Value;
                }
            }
        }

        public void ClearEventTasks(Guid jobId, int eventId, int? expireAfterDays = null)
        {
            using (var sqlConnection = new SqlConnection(_nServiceBusState))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText = "nsb.ebmsClearEventTasks";
                    sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    sqlCommand.Parameters.Add(new SqlParameter("@JobId", jobId));
                    sqlCommand.Parameters.Add(new SqlParameter("@EventId", eventId));
                    if (expireAfterDays.HasValue)
                        sqlCommand.Parameters.Add(new SqlParameter("@ExpireDays", expireAfterDays.Value));
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }
    }
}
