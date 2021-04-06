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
            [TimerTrigger("0 */5 7-23 * * *")] TimerInfo myTimer,
            ILogger log)
        {
            // get list of projects from environment variable
            string projectsEnvVar = Environment.GetEnvironmentVariable("PROJECTS") ?? throw new NullReferenceException("Missing PROJECTS environment variable");
            var todoistProjectsToTraverse = projectsEnvVar.Split(",").ToList();
            todoistProjectsToTraverse.ForEach(p => p?.ToLower().Trim());

            // get project Ids from todoist of projects to traverse
            ITodoistClient client = new TodoistClient(Environment.GetEnvironmentVariable("TODOIST_APIKEY"));
            IEnumerable<Project>? tdiProjects = await client.Projects.GetAsync();
            var tdiProjectIds = tdiProjects
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .Where(p => todoistProjectsToTraverse.Contains(p.Name.ToLower()))
                .Select(p => p.Id)
                .ToList();

            IEnumerable<Item>? tdiAllItems = await client.Items.GetAsync();
            var tdiItemsToProcess = tdiAllItems.Where(i => tdiProjectIds.Contains(i.ProjectId ?? 0)).ToList();

            // traverse each item for existing Regex
            string regex = @"\ +\[+\d+\/*\d*\ days\ remaining\]+";
            bool? workWeekOnly = WorkWeekOnly();
            foreach (Item item in tdiItemsToProcess.Where(i => i?.DueDate?.Date.HasValue ?? false))
            {
                DateTime dueDate = item.DueDate.Date ?? throw new NullReferenceException($"Date is found null on item with id {item.Id}");
                (int days, int workDays) = CalculateDays(dueDate);
                string daysDisplay = workWeekOnly switch
                {
                    true => workDays.ToString(),
                    false => days.ToString(),
                    _ => $"{workDays}/{days}"
                };
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
                item.DueDate = new DueDate(item.DueDate.Date.Value.ToString("yyyy-MM-dd"), null, item.DueDate.Language);
                await client.Items.UpdateAsync(item);
            }


            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
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
        private static bool? WorkWeekOnly()
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
