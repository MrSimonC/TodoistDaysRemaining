using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TodoistShared;

namespace TodoistConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider? serviceProvider = serviceCollection.BuildServiceProvider();
            ILogger<Program>? log = serviceProvider.GetService<ILogger<Program>>();

            log.LogInformation($"C# Timer console app executed at: {DateTime.Now}");
            await ProcessTodoist.ProcessTodoistAsync(log);
            log.LogInformation("Process finished");

        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole())
                .AddTransient<Program>();
        }
    }
}
