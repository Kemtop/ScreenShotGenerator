using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Entities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.perfomenceService
{
    /// <summary>
    /// Мониторинг производительности системы,работающий в фоне.
    /// </summary>
    public class perfomanceService : IHostedService
    {
        private readonly IServiceScopeFactory scopeFactory;
               
        public perfomanceService(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {                  
          Task.Run(() => monitoringPerfomances(cancellationToken));
            return Task.CompletedTask;
        }

      
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task monitoringPerfomances(CancellationToken cancellationToken)
        {
            Log.Information("Run service monitoring perfomaces.");

            while (!cancellationToken.IsCancellationRequested)
            {
                
                await Task.Delay(10000);
                double cpuUsage = await GetCpuUsageForProcess();
                //В байтах.
                long memoryAlloc = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;

                //Добавляем строку в таблицу.
                mPerformanceInfo line = new mPerformanceInfo();
                line.cpuLoad = (float)cpuUsage;
                line.memoryUsage =(int)memoryAlloc;
                line.date = DateTime.Now;

                //_dbContext.performanceInfo.Add(line);

                //Добавить сохранение в базу данных каждые...n времени.

                using (var scope = scopeFactory.CreateScope())
                {
                    ApplicationDbContext _dbContext1 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    _dbContext1.performanceInfo.Add(line);
                    _dbContext1.SaveChanges();
                }


                // string _cpuUsage = String.Format("{0:0.00}", cpuUsage);

                Console.WriteLine(cpuUsage);
            }
            
        }

             
        private async Task<double> GetCpuUsageForProcess()
        {

            //Альтернатива-https://github.com/devizer/Universe.CpuUsage
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            await Task.Delay(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }

    }
}
