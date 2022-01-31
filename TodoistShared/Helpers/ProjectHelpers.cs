using Todoist.Net;

namespace TodoistShared.Helpers;

public static partial class ProjectHelpers
{
    public static async Task<List<Item>> GetTodoistProjectItems(List<ComplexId> tdiProjectIds, ITodoistClient client, ILogger log)
    {
        IEnumerable<Item>? tdiAllItems = await client.Items.GetAsync();
        var tdiItemsToProcess = tdiAllItems.Where(i => tdiProjectIds.Contains(i.ProjectId ?? 0)).ToList();
        log.LogInformation("Found {count} items to process", tdiItemsToProcess.Count);
        return tdiItemsToProcess;
    }

    public static async Task<List<ComplexId>> GetTodoistProjectIds(List<string> todoistProjectsToTraverse, ITodoistClient client, ILogger log)
    {
        IEnumerable<Project>? tdiProjects = await client.Projects.GetAsync();
        var tdiProjectIds = tdiProjects
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .Where(p => todoistProjectsToTraverse.Contains(p.Name.ToLower()))
            .Select(p => p.Id)
            .ToList();
        log.LogInformation("Found {count} projects to process", tdiProjectIds.Count);
        return tdiProjectIds;
    }

    public static List<string> GetListOfProjectsFromConfig(string projectList, ILogger log)
    {
        List<string> todoistProjectsToTraverse = projectList
            .Split(",")
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToList();
        log.LogInformation("Found projects from config: {projects}", string.Join(", ", todoistProjectsToTraverse));
        return todoistProjectsToTraverse;
    }

    public static async Task<List<Item>> GetItemsFromProjectList(string projects, ITodoistClient client, ILogger log)
    {
        List<string> todoistProjectsToTraverse = GetListOfProjectsFromConfig(projects, log);
        List<ComplexId> todoistProjectIds = await GetTodoistProjectIds(todoistProjectsToTraverse, client, log);
        List<Item> todoistItemsToProcess = await GetTodoistProjectItems(todoistProjectIds, client, log);
        return todoistItemsToProcess;
    }
}
