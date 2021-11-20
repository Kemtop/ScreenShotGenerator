using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Entities;
using ScreenShotGenerator.Services;
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
        /// <summary>
        /// Период мониторинга,в секундах.
        /// </summary>
        private int intervalMonitoring;

        //Интервал очистки в днях.
        private int periodClearData;

        /// <summary>
        /// Таймер очистки таблицы БД.
        /// </summary>
        private Timer clearTimer;


        public perfomanceService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            this.scopeFactory = scopeFactory;
            
            //Время мониторинга в секундах.
            if (!Int32.TryParse(configuration["perfomanceService:intervalMonitoring"], out intervalMonitoring))
            {
                Log.Error("Can't convert perfomanceService:intervalMonitoring to Int32. Bad value:" +
                   configuration["perfomanceService:intervalMonitoring"] + ". Set deafault value 90.");
                intervalMonitoring = 90;
            }

          
            if (!Int32.TryParse(configuration["perfomanceService:periodClearData"], out periodClearData))
            {
                Log.Error("Can't convert perfomanceService:periodClearData to Int32. Bad value:" +
                   configuration["perfomanceService:periodClearData"] + ". Set deafault value 31.");
                periodClearData = 31;//В днях.
            }

            //Запускать после старта системы и повторять с таким же интервалом.
            int runCheckOldDataAfter;
            if (!Int32.TryParse(configuration["perfomanceService:runCheckOldDataAfter"], out runCheckOldDataAfter))
            {
                Log.Error("Can't convert perfomanceService:runCheckOldDataAfter to Int32. Bad value:" +
                   configuration["perfomanceService:runCheckOldDataAfter"] + ". Set deafault value 600.");
                runCheckOldDataAfter = 600;
            }


            runCheckOldDataAfter *= 60000;
            clearTimer = new Timer((object stateInfo)=> {
                clearDataInDb();
            },null, runCheckOldDataAfter, runCheckOldDataAfter);
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

        /// <summary>
        /// Метод работающий в отдельном потоке. Переодически мониторит состояние системы.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task monitoringPerfomances(CancellationToken cancellationToken)
        {
            Log.Information("Run service monitoring perfomaces.");
            /*
            Log.Information("Monitoring disable by programmer!");
            int y = 10; //Временная отладка.
            if (y == 10) return;
            */

            while (!cancellationToken.IsCancellationRequested)
            {                
                await Task.Delay(intervalMonitoring*1000);
                double cpuUsage = await GetCpuUsageForProcess();
                //В байтах.
                long memoryAlloc = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;

                //Добавляем строку в таблицу.
                mPerformanceInfo line = new mPerformanceInfo();
                line.cpuLoad = (float)cpuUsage;
                line.memoryUsage =(int)memoryAlloc;
                line.date = DateTime.Now;


                //Получаю количество ожидающих задач.
                using (var scope = scopeFactory.CreateScope())
                {
                    var sc=scope.ServiceProvider.GetRequiredService<IScreenShoter>();
                    line.poolWaiterTask = sc.getWaitTasksCnt();
                }


                //Добавить сохранение в базу данных каждые...n времени.
                using (var scope = scopeFactory.CreateScope())
                {
                    ApplicationDbContext _dbContext1 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    _dbContext1.performanceInfo.Add(line);
                    _dbContext1.SaveChanges();
                }

                // string _cpuUsage = String.Format("{0:0.00}", cpuUsage);
            }

            Log.Information("Stop service monitoring perfomaces.");

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


        /// <summary>
        /// Очищает старые данные в базе данных.
        /// </summary>
        private void clearDataInDb()
        {
            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext _dbContext1 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                int cnt=_dbContext1.performanceInfo.Where(x=>x.date.AddDays(periodClearData)<DateTime.Now).Count();

                if (cnt == 0) return; //Не чего очищать.

                _dbContext1.performanceInfo.RemoveRange(
                    _dbContext1.performanceInfo.Where(x => x.date.AddDays(periodClearData) < DateTime.Now)
                    );

                Log.Information("Clear performanceInfo table's old data. Clear Items="+cnt.ToString());
                _dbContext1.SaveChanges();
            }

        }
    }
}
