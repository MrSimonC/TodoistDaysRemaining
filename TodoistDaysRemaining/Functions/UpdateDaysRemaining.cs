namespace TodoistFunctions.Functions;

public class UpdateDaysRemaining
{
    [FunctionName("UpdateDaysRemaining")]
    public async Task RunAsync(
        [TimerTrigger("0 0 6-23 * * *"
            #if DEBUG
                , RunOnStartup =true
	        #endif
            )] TimerInfo myTimer,
        ILogger log)
    {
        log.LogInformation("Function code running");
        var process = new ProcessTodoist();
        await process.ProcessTodoistAsync(log);
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
    }
}