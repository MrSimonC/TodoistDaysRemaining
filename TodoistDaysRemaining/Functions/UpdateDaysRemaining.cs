using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TodoistShared;

namespace TodoistDaysRemaining.Functions
{
    public static class UpdateDaysRemaining
    {
        [FunctionName("UpdateDaysRemaining")]
        public static async Task RunAsync(
            [TimerTrigger("0 0 6-23 * * *"
            #if DEBUG
                , RunOnStartup =true
	        #endif
            )] TimerInfo myTimer,
            ILogger log)
        {
            log.LogInformation("Function code running");
            await ProcessTodoist.ProcessTodoistAsync(log);
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
