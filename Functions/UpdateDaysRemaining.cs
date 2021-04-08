using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Todoist.Net;
using Todoist.Net.Models;

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
            ITodoistClient client = new TodoistClient(Environment.GetEnvironmentVariable("TODOIST_APIKEY"));

            List<string> todoistProjectsToTraverse = GetListOfProjectsFromConfig(log);
            List<ComplexId> todoistProjectIds = await GetTodoistProjectIds(log, todoistProjectsToTraverse, client);
            List<Item> todoistItemsToProcess = await GetTodoistProjectItems(log, client, todoistProjectIds);

            // traverse each item for existing Regex
            string regex = @"\ +\[+\d+\/*\d*\ days\ remaining\]+";
            bool? workWeekOnly = GetWorkWeekOnlyFromConfig();
            log.LogInformation($"WorkWeekOnly set to {workWeekOnly}");

            foreach (Item item in todoistItemsToProcess.Where(i => i?.DueDate?.Date.HasValue ?? false))
            {
                log.LogInformation($"Looking at item: {item.Content}");
                DateTime dueDate = item.DueDate.Date ?? throw new NullReferenceException($"Date is found null on item with id {item.Id}");
                (int days, int workDays) = CalculateDays(dueDate);
                string daysDisplay = workWeekOnly switch
                {
                    true => workDays.ToString(),
                    false => days.ToString(),
                    _ => $"{days}/{workDays}"
                };
                string update = GetUpdateText(workWeekOnly, days, workDays, daysDisplay);
                log.LogInformation($"{item.Content}, now: {DateTime.Now.Date} to due date: {item.DueDate.Date} is {days}/{workDays}");
                if (Regex.IsMatch(item.Content, regex))
                {
                    item.Content = Regex.Replace(item.Content, regex, update);
                }
                else
                {
                    item.Content += $"{update}";
                }
                log.LogInformation(item.Content);

                // item.DueDate.StringDate (ironically inaccessible) seems to hold item.DueDate.Date in UTC, which overrides item.DueDate.Date
                // which when in BST changes the due date back to the day before (at 11pm). Therefore override this by creating a new instance.
                string dueDateString = item.DueDate.Date.Value.ToString("yyyy-MM-dd");
                item.DueDate = new DueDate(dueDateString, null, item.DueDate.Language);
#if DEBUG
                Console.WriteLine($"{item.Content} with due date: {dueDateString}");
#else
                await client.Items.UpdateAsync(item);
#endif
            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        private static string GetUpdateText(bool? workWeekOnly, int days, int workDays, string daysDisplay)
        {
            string update = $" [{daysDisplay} days remaining]";

            if (workWeekOnly ?? true)
            {
                if (workDays <= 0)
                {
                    update = string.Empty;
                }
            }
            else
            {
                if (days <= 0)
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
            var todoistProjectsToTraverse = projectsEnvVar.Split(",").ToList();
            todoistProjectsToTraverse.ForEach(p => p?.ToLower().Trim());
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
                .Range(1, days)
                .Select(x => DateTime.Now.Date.AddDays(x))
                .Count(x => x.DayOfWeek != System.DayOfWeek.Saturday && x.DayOfWeek != System.DayOfWeek.Sunday);

            return (days, workDays);
        }

        /// <summary>
        /// See if we want to only count work days. If null, then include both all days and work days.
        /// </summary>
        /// <returns>true=yes workdays only, false=all days, null=show both</returns>
        private static bool? GetWorkWeekOnlyFromConfig()
        {
            string? workWeekEnv = Environment.GetEnvironmentVariable("WORKWEEK");
            if (workWeekEnv is null)
            {
                return null;
            }
            bool.TryParse(workWeekEnv, out bool workWeek);
            return workWeek;
        }
    }
}
