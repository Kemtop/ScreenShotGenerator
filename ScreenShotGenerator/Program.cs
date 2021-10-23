using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScreenShotGenerator
{
    public class Program
    {
       
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            using (var scope=host.Services.CreateScope())
            {
                DatabeseInitialization.InitAsync(scope.ServiceProvider);
            }


                host.Run();

            //https://stackoverflow.com/questions/41675577/where-can-i-log-an-asp-net-core-apps-start-stop-error-events
            //host.WaitForShutdownAsync();
            System.Diagnostics.Debug.Write("Stop");

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });//.ConfigureServices(services=>services.AddHostedService<BackgroundWorker>());

     


    }


}
