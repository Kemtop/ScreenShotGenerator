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
    /// Фоновая задача создания скриншотов.
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

        public Task StopAsync(CancellationToken cancellationToken)
        {
             _screenShoter.stopService(cancellationToken);
            return Task.CompletedTask;
        }

    }
}
