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
        public BackgroundWorker(IScreenShoter screenShoter)
        {          
            _screenShoter = screenShoter;
        }

        public  Task StartAsync(CancellationToken cancellationToken)
        {
             _screenShoter.runService(cancellationToken);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Не работает и похоже это баг Microsoft, ждем обновлений.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Write("Stop");
             _screenShoter.stopService(cancellationToken);
            return Task.CompletedTask;
        }

    }
}
