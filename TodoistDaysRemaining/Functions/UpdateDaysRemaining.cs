using TodoistShared;
using static TodoistShared.Helpers.EnvironmentHelpers;

namespace TodoistFunctions.Functions;

public class UpdateDaysRemaining
{
    [Function("UpdateDaysRemaining")]
    public async Task RunAsync(
        [TimerTrigger("0 0 6-23 * * *"
            #if DEBUG
                , RunOnStartup =true
	        #endif
            )] TimerInfo myTimer,
            FunctionContext executionContext)
    {
        var log = executionContext.GetLogger("UpdateDaysRemaining");
        log.LogInformation("Function {name} code running", nameof(UpdateDaysRemaining));

        string todoistAPIKey = Environment.GetEnvironmentVariable("TODOIST_APIKEY") ?? throw new NullReferenceException("Missing TODOIST_APIKEY environment variable");
        bool forceWrite = GetBoolFromEnvVar("FORCE_WRITE");
        string projectsDueDate = Environment.GetEnvironmentVariable("PROJECTS_DUE_DATE") ?? throw new NullReferenceException("Missing PROJECTS environment variable");
        string projectsCompletePastEvents = Environment.GetEnvironmentVariable("PROJECTS_COMPLETE_PAST_EVENTS") ?? throw new NullReferenceException("Missing PROJECTS environment variable");

        var todoistShared = new TodoistSharedLogic(todoistAPIKey, log);
        await todoistShared.CompletePastEntriesAsync(projectsCompletePastEvents);
        await todoistShared.AddDueDaysAsync(projectsDueDate, forceWrite);

        log.LogInformation("Function {name} finished at {date}", nameof(UpdateDaysRemaining), DateTime.Now);
        // minor update to trigger CICD
    }
}