using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace OASystems.ITRBroker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            List<ITRTask> list = new List<ITRTask>();
            list.Add(new ITRTask() { Name = "Job A", Schedule = "*/5 * * * * *" });
            list.Add(new ITRTask() { Name = "Job B", Schedule = "*/10 * * * * *" });

            CreateHostBuilder(args, list).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, List<ITRTask> list) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    foreach (ITRTask itrTask in list)
                    {
                        services.AddSingleton<IHostedService>(serviceProvider => new Worker(serviceProvider.GetService<ILogger<Worker>>(), itrTask.Name, itrTask.Schedule));
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
