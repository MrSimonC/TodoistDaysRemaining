using DayOfWeek = Todoist.Net.Models.DayOfWeek;

namespace TodoistShared.Helpers;

public static class DateLogic
{
    /// <summary>
    /// Calculate both days and work days from now till <paramref name="date"/>
    /// </summary>
    /// <param name="date">future due date</param>
    /// <returns>tuple of total days, work days</returns>
    public static (int days, int workDays) CalculateDays(DateTime date)
    {
        if (DateTime.Now.Date >= date.Date)
        {
            return (0, 0);
        }

        int days = (int)(date - DateTime.Now.Date).TotalDays;
        int workDays = Enumerable
            .Range(1, days - 1)  // days-1 since we don't want to count the final day if it's a workday
            .Select(x => DateTime.Now.Date.AddDays(x))
            .Count(x => (DayOfWeek)x.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday);

        return (days, workDays);
    }
}
