using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Todoist.Net;
using static TodoistShared.Helpers.DateLogic;
using static TodoistShared.Helpers.ProjectHelpers;

namespace TodoistShared;

/// <summary>
/// Process todolist items.
/// </summary>
public partial class TodoistSharedLogic
{
    private readonly string TodoistAPIKey = string.Empty;
    private readonly ITodoistClient client;
    private readonly ILogger log;

    public TodoistSharedLogic(string apiKey, ILogger logger)
    {
        TodoistAPIKey = apiKey;
        client = new TodoistClient(TodoistAPIKey);
        log = logger;
    }

    [LoggerMessage(0, LogLevel.Information, "{memberName}(),{uniqueKey}: {message}")]
    partial void LogInfo(string message, string uniqueKey, [CallerMemberName] string memberName = "");

    public async Task AddDueDaysAsync(string projects, bool forceWrite)
    {
        List<Item> todoistItemsToProcess = await GetItemsFromProjectList(projects, client, log);
    
        string regex = @"\ +\[+(\d+)\/*\d*\ days\ remaining\]+";  // Group 1: Total normal days remaining
        foreach (Item item in todoistItemsToProcess.Where(i => i?.DueDate?.Date.HasValue ?? false))
        {
            LogInfo("Looking at item.", item.Content);
            DateTime dueDate = item.DueDate.Date ?? throw new NullReferenceException($"Date is found null on item with id {item.Id}");
            (int days, int workDays) = CalculateDays(dueDate);
            LogInfo($"Found days: {days}/{workDays}", item.Content);

            // skip hitting the API to update entry which needs no update
            if (Regex.IsMatch(item.Content, regex))
            {
                try
                {
                    Match m = Regex.Match(item.Content, regex);
                    if (!int.TryParse(m.Groups[1]?.Value, out int existingDays))
                    {
                        log.LogWarning("{content}: Couldn't parse the days out of existing entry.",item.Content);
                        continue;
                    }
                    LogInfo($"Checking existing entries for changes. Comparing existing {existingDays} days to calculated {days} days", item.Content);
                    if (existingDays == days && !forceWrite)
                    {
                        LogInfo("Skipping entry as days don't need update.", item.Content);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error when traversing existing entry day with content: {content}", item.Content);
                    continue;
                }
            }

            string update = $" [{days}/{workDays} days remaining]";
            LogInfo($"Now: {DateTime.Now.Date} to due date: {item.DueDate.Date} is {days}/{workDays}", item.Content);
            if (Regex.IsMatch(item.Content, regex))
            {
                LogInfo("Regex was matched. Updating existing entry figures.", item.Content);
                item.Content = Regex.Replace(item.Content, regex, update);
            }
            else
            {
                LogInfo("Regex was not matched. Updating entry with new text.", item.Content);
                item.Content += $"{update}";
            }

            // item.DueDate.StringDate (ironically inaccessible) seems to hold item.DueDate.Date in UTC, which overrides item.DueDate.Date
            // which when in BST changes the due date back to the day before (at 11pm). Therefore override this by creating a new instance.
            string dueDateString = item.DueDate.Date.Value.ToString("yyyy-MM-dd");
            item.DueDate = new DueDate(dueDateString, null, item.DueDate.Language);
            LogInfo($"Due date: {dueDateString}", item.Content);
#if !DEBUG
            await client.Items.UpdateAsync(item);
#endif
        }
    }

    /// <summary>
    /// Complete any date yesterday or before, or today if it has a time (not midnight) which has passed.
    /// </summary>
    /// <param name="projects">comma delimited string of projects to traverse</param>
    /// <returns>A tasl</returns>
    public async Task CompletePastEntriesAsync(string projects)
    {
        List<Item> todoistItemsToProcess = await GetItemsFromProjectList(projects, client, log);
        foreach (Item item in todoistItemsToProcess.Where(i => i?.DueDate?.Date.HasValue ?? false))
        {
            var dueDateUtc = item.DueDate.Date!.Value.ToUniversalTime();
            if (dueDateUtc.Date < DateTime.UtcNow.Date
                || (dueDateUtc < DateTime.UtcNow && dueDateUtc.TimeOfDay != new TimeSpan()))
            {
                LogInfo($"Item is in the past with date {item.DueDate.Date.Value:G}", item.Content);
#if !DEBUG
                        await client.Items.CloseAsync(item.Id);
#endif
                continue;

            }
        }
    }
}
