using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.perfomenceService;
using ScreenShotGenerator.Services;
using Serilog;
using Serilog.Events;


namespace ScreenShotGenerator
{
    public class Program
    {

        public static void Main(string[] args)
        {
            /*
            SwapMonitor sm = new SwapMonitor();
            sm.TestGetSystemctlInfo();
            int y = 0;
            if (y == 0) return;
            */

            //������� ������������,��� ���������  Kestrel.
            IConfigurationRoot config_ = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                 .Build();

            //��������� serilog.  
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Information() 
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) //�������� ������ �������� Microsoft.
           .WriteTo.File(      
                @"./Logs/log.txt",
                shared: true, //�������� ���� ���������.
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
                    //�������������� ������������� ������������� � �������� ��������, ���� ���������.
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

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {                  
                    webBuilder.UseStartup<Startup>();
                }).UseSerilog() //������� �����.
            //������ ����������� ������������������.
         .ConfigureServices(s => s.AddHostedService<perfomanceService>())
        //������ �������.
        .ConfigureServices(services => services.AddHostedService<BackgroundWorker>());

 
    }


    
}
