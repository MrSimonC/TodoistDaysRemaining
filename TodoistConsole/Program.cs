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


            Console.WriteLine("code running");
            await ProcessTodoist.ProcessTodoistAsync(log);
            Console.WriteLine("code running");

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole())
                .AddTransient<Program>();
        }
    }
}
