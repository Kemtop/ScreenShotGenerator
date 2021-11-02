using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.perfomenceService;
using Serilog;
using Serilog.Events;


namespace ScreenShotGenerator
{
    public class Program
    {
        static IConfigurationRoot config_;

        public static void Main(string[] args)
        {
            //Получаю конфигурацию,для настройки  Kestrel.
            IConfigurationRoot config_ = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                 .Build();

            //Подключаю serilog.  
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Information() 
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) //Выводить только варнинги Microsoft.
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
                    //Первоначальная инициализация пользователей и значение настроек, если требуется.
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
                    //Настройки Kestrel, для использования https.
                    webBuilder.ConfigureKestrel(options => {

                        try
                        {
                            Log.Error("null");

                            string pfxFilePath = "certificate.pfx"; //config_["ConfigureKestrel:pfxFilePath"];
                            int httpsPort = 5001;//Convert.ToInt32(config_["ConfigureKestrel:httpsPort"]);
                            string pfxPassword = "123456Kz";// config_["ConfigureKestrel:pfxPassword"];

                            options.Listen(IPAddress.Any, 5000); //HTTP port
                            options.Listen(IPAddress.Loopback, 5001, listenOptions =>
                            {
                                listenOptions.UseHttps("certificate.pfx", pfxPassword);
                            });

                            /*
                            options.Listen(IPAddress.Any, httpsPort, listenOptions => {
                                //Включить поддержку HTTP1 и HTTP2 (требуется, если вы хотите разместить конечные точки gRPC)
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                //Настраиваю Kestrel использовать локальный сертификат(.PFX файл) для хостинга HTTPS.
                                listenOptions.UseHttps(pfxFilePath, pfxPassword);
                            });*/


                        }
                        catch(Exception ex)
                        {
                            Log.Error("Exception to read Kestrel settings:" + ex.Message);
                        }                      
               
                    });

                    webBuilder.UseStartup<Startup>();
                }).UseSerilog() //Ведение логов.
            //Сервис мониторинга производительности.
         .ConfigureServices(s => s.AddHostedService<perfomanceService>())
        //Сервис скринов.
        .ConfigureServices(services => services.AddHostedService<BackgroundWorker>());

 
    }


    
}
