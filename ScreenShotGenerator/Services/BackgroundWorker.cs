using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator
{
    /// <summary>
    /// Фоновая задача после запуска приложения.
    /// </summary>
    internal class BackgroundWorker : IHostedService
    {
      
        //Интерфейс скрин шоттера.
        private readonly IScreenShoter _screenShoter;
        private readonly ApplicationDbContext dbContext;// = new DatabaseContext();

        public BackgroundWorker(IScreenShoter screenShoter)
        {          
            _screenShoter = screenShoter;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _screenShoter.runService(cancellationToken);
        }


        /// <summary>
        /// Не работает и похоже это баг Microsoft, ждем обновлений.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Write("Stop");
            return _screenShoter.stopService(cancellationToken);
           // return Task.CompletedTask;
        }


        /*
        public void Dispose()
        {
            timer?.Dispose();
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(o => {
                Interlocked.Increment(ref number);
                logger.LogInformation($"Printing the worker number {number}");
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }
        */


    }
}
