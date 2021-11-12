namespace TodoistFunctions.Functions;

public static class UpdateDaysRemaining
{
#if DEBUG
    const bool runOnStartUp = true;
#else
    const bool runOnStartUp = false;
#endif

    [FunctionName("UpdateDaysRemaining")]
    public static async Task RunAsync(
        [TimerTrigger("0 0 6-23 * * *", RunOnStartup = runOnStartUp)] TimerInfo myTimer,
        ILogger log)
    {
        log.LogInformation("Function code running");
        await ProcessTodoist.ProcessTodoistAsync(log);
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
    }
}
