using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Todoist.Net;
using Todoist.Net.Models;

namespace TodoistFunctions.Todoist;

/// <summary>
/// Process todolist items. Needed env vars: TODOIST_APIKEY, PROJECTS, WORKWEEK (optional)
/// </summary>
public class ProcessTodoist
{
    private readonly bool CompletePastItems;
    private readonly string TodoistAPIKey = string.Empty;

    public ProcessTodoist()
    {
        TodoistAPIKey = Environment.GetEnvironmentVariable("TODOIST_APIKEY") ?? throw new NullReferenceException("Missing TODOIST_APIKEY environment variable");
        string completePastItemsString = Environment.GetEnvironmentVariable("COMPLETE_PAST_ITEMS") ?? throw new NullReferenceException("Missing COMPLETE_PAST_ITEMS environment variable");
        if (!bool.TryParse(completePastItemsString, out CompletePastItems))
        {
            throw new ArgumentException(nameof(completePastItemsString));
        }
        CompletePastItems = bool.Parse(completePastItemsString);
    }

    public async Task ProcessTodoistAsync(ILogger log)
    {
        ITodoistClient client = new TodoistClient(TodoistAPIKey);
        // Force writing to all todoist entries (change from true to false directly in Portal > Configuration since SetEnvironmentVariable won't work in Azure Functions
        const string forceWriteOnceEnvVarName = "FORCE_WRITE_ONCE";
        bool forceWrite = GetBoolFromEnvVar(forceWriteOnceEnvVarName) ?? false;

        List<string> todoistProjectsToTraverse = GetListOfProjectsFromConfig(log);
        List<ComplexId> todoistProjectIds = await GetTodoistProjectIds(log, todoistProjectsToTraverse, client);
        List<Item> todoistItemsToProcess = await GetTodoistProjectItems(log, client, todoistProjectIds);

        // traverse each item for existing Regex
        string regex = @"\ +\[+(\d+)\/*\d*\ days\ remaining\]+";
        // See if we want to only count work days. If null, then include both all days and work days.
        // true=yes workdays only, false=all days, null=show both
        bool? workWeekOnly = GetBoolFromEnvVar("WORKWEEK");
        log.LogInformation($"WorkWeekOnly set to {workWeekOnly}");

        foreach (Item item in todoistItemsToProcess.Where(i => i?.DueDate?.Date.HasValue ?? false))
        {
            if (item.DueDate.Date!.Value.ToUniversalTime() < DateTime.UtcNow)
            {
                log.LogInformation($"Item: {item.Content} is in the past with date {item.DueDate.Date.Value:dd-MM-yyyy HH:mm}");
                if (CompletePastItems)
                {
                    log.LogInformation($"CompletePastItems is true. Complete Item: {item.Content}.");
#if !DEBUG
                    var completeItemArg = new CompleteItemArgument(item.Id, DateTime.UtcNow);
                    await client.Items.CompleteAsync(completeItemArg);
#endif
                    continue;
                }
            }

            log.LogInformation($"Looking at item: {item.Content}");
            DateTime dueDate = item.DueDate.Date ?? throw new NullReferenceException($"Date is found null on item with id {item.Id}");
            (int days, int workDays) = CalculateDays(dueDate);
            log.LogInformation($"Found days: {days}/{workDays}");

            // skip hitting the API to update entry which needs no update
            if (Regex.IsMatch(item.Content, regex))
            {
                try
                {
                    Match m = Regex.Match(item.Content, regex);
                    bool gotExistingDays = int.TryParse(m.Groups[1]?.Value, out int existingDays); // ...[12/34 or ...[12..
                    if (!gotExistingDays)
                    {
                        log.LogWarning($"Couldn't parse the days out of existing entry {item.Content}");
                        continue;
                    }
                    int calculatedDays = workWeekOnly ?? false ? workDays : days;
                    log.LogInformation($"Checking existing entries for changes. Comparing existing {existingDays} days to calculated {calculatedDays} days");
                    if ((existingDays == calculatedDays) && !forceWrite)
                    {
                        log.LogInformation($"Skipping entry as days don't need update. Entry is: {item.Content}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, $"Error when traversing existing entry day with content: {item.Content}");
                    continue;
                }
            }

            string daysDisplay = workWeekOnly switch
            {
                true => workDays.ToString(),
                false => days.ToString(),
                _ => $"{days}/{workDays}"
            };
            string update = GetUpdateText(workWeekOnly, days, workDays, daysDisplay);
            log.LogInformation($"{item.Content}: {nameof(GetUpdateText)} returned {update}");
            log.LogInformation($"{item.Content}, now: {DateTime.Now.Date} to due date: {item.DueDate.Date} is {days}/{workDays}");
            if (Regex.IsMatch(item.Content, regex))
            {
                log.LogInformation("Regex was matched. Updating existing entry figures.");
                item.Content = Regex.Replace(item.Content, regex, update);
            }
            else
            {
                log.LogInformation("Regex was not matched. Updating entry with new text.");
                item.Content += $"{update}";
            }
            log.LogInformation(item.Content);

            // item.DueDate.StringDate (ironically inaccessible) seems to hold item.DueDate.Date in UTC, which overrides item.DueDate.Date
            // which when in BST changes the due date back to the day before (at 11pm). Therefore override this by creating a new instance.
            string dueDateString = item.DueDate.Date.Value.ToString("yyyy-MM-dd");
            item.DueDate = new DueDate(dueDateString, null, item.DueDate.Language);
#if DEBUG
            log.LogInformation($"{item.Content} with due date: {dueDateString}");
#else
                log.LogInformation("Writing to Todoist");
                await client.Items.UpdateAsync(item);
#endif
        }
    }

    private static string GetUpdateText(bool? workWeekOnly, int days, int workDays, string daysDisplay)
    {
        string update = $" [{daysDisplay} days remaining]";

        if (!(workWeekOnly ?? false)) // days will always be bigger than workdays, so only blank string on days consideration if workWeekOnly == null
        {
            if (days <= 0)
            {
                update = string.Empty;
            }
        }
        else
        {
            if (workDays <= 0)
            {
                update = string.Empty;
            }
        }

        return update;
    }

    private static async Task<List<Item>> GetTodoistProjectItems(ILogger log, ITodoistClient client, List<ComplexId> tdiProjectIds)
    {
        IEnumerable<Item>? tdiAllItems = await client.Items.GetAsync();
        var tdiItemsToProcess = tdiAllItems.Where(i => tdiProjectIds.Contains(i.ProjectId ?? 0)).ToList();
        log.LogInformation($"Found {tdiItemsToProcess.Count} items to process");
        return tdiItemsToProcess;
    }

    private static async Task<List<ComplexId>> GetTodoistProjectIds(ILogger log, List<string> todoistProjectsToTraverse, ITodoistClient client)
    {
        IEnumerable<Project>? tdiProjects = await client.Projects.GetAsync();
        var tdiProjectIds = tdiProjects
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .Where(p => todoistProjectsToTraverse.Contains(p.Name.ToLower()))
            .Select(p => p.Id)
            .ToList();
        log.LogInformation($"Found count of projects from todoist: {tdiProjectIds.Count()}");
        return tdiProjectIds;
    }

    private static List<string> GetListOfProjectsFromConfig(ILogger log)
    {
        string projectsEnvVar = Environment.GetEnvironmentVariable("PROJECTS") ?? throw new NullReferenceException("Missing PROJECTS environment variable");
        List<string> todoistProjectsToTraverse = projectsEnvVar
            .Split(",")
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToList();
        log.LogInformation($"Found projects from config: {string.Join(", ", todoistProjectsToTraverse)}");
        return todoistProjectsToTraverse;
    }

    /// <summary>
    /// Calculate both days and work days from now till <paramref name="date"/>
    /// </summary>
    /// <param name="date">future due date</param>
    /// <returns>tuple of total days, work days</returns>
    public static (int days, int workDays) CalculateDays(DateTime date)
    {
        if (DateTime.Now.Date >= date)
        {
            return (0, 0);
        }

        int days = (int)(date - DateTime.Now.Date).TotalDays;
        int workDays = Enumerable
            .Range(1, days - 1)  // days-1 since we don't want to count the final day if it's a workday
            .Select(x => DateTime.Now.Date.AddDays(x))
            .Count(x => x.DayOfWeek != System.DayOfWeek.Saturday && x.DayOfWeek != System.DayOfWeek.Sunday);

        return (days, workDays);
    }

    /// <summary>
    /// Get nullable bool from environment variable
    /// </summary>
    private static bool? GetBoolFromEnvVar(string envVarName)
    {
        string? result = Environment.GetEnvironmentVariable(envVarName);
        if (result is null)
        {
            return null;
        }
        bool.TryParse(result, out bool workWeek);
        return workWeek;
    }
}
