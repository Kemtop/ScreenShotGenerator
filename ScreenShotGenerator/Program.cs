using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenShotGenerator.perfomenceService;
using Serilog;
using Serilog.Events;





namespace ScreenShotGenerator
{
    public class Program
    {

        public static void Main(string[] args)
        {
            //Подключаю serilog.  
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Information()
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
           .WriteTo.Console()
           .WriteTo.File(      
                @"./Logs/log.txt",
                shared: true, //Доступен всем процессам.
                rollingInterval: RollingInterval.Day,
                flushToDiskInterval: TimeSpan.FromSeconds(20),
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
           .CreateLogger();


            try
            {
                Log.Information("[--------------------------Starting web host-----------------------------]");
                var host = CreateHostBuilder(args).Build();
                using (var scope = host.Services.CreateScope())
                {
                    DatabeseInitialization.InitAsync(scope.ServiceProvider);
                }

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly. Exeption:"+ex.Message);
                
            }
            finally
            {
                Log.CloseAndFlush();
            }


            //https://stackoverflow.com/questions/41675577/where-can-i-log-an-asp-net-core-apps-start-stop-error-events
            //host.WaitForShutdownAsync();
           // System.Diagnostics.Debug.Write("Stop");

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                }).UseSerilog() //Ведение логов.
            //Сервис мониторинга производительности.
         .ConfigureServices(s => s.AddHostedService<perfomanceService>())
        //Сервис скринов.
        .ConfigureServices(services => services.AddHostedService<BackgroundWorker>());

 
    }


    
}
