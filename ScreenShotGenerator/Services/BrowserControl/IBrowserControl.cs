
using ScreenShotGenerator.Services.ScreenShoterPools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Интерфейс для управления браузерами через Selenium.
    /// </summary>
   public interface IBrowserControl
    {

        /// <summary>
        /// Количество задач из пула которые браузер обрабатывает за раз.
        /// </summary>
        public int tasksPerThread { get; set; }

        /// <summary>
        /// Идентификатор браузера.
        /// </summary>
        public int browserId { get; set; }

        /// <summary>
        /// Задает таймауты браузера.
        /// </summary>
        /// <param name="pageLoadTimeouts"></param>
        /// <param name="javaScriptTimeouts"></param>
        void setTimeouts(int pageLoadTimeouts, int javaScriptTimeouts);

        /// <summary>
        /// Обработка задач в потоке задач. Выполняется бесконечно.
        /// Управляющий процесс запускает в отдельном потоке.
        /// </summary>
        /// <param name="poolTasks"></param>
        void processPool(ref poolTasks pool,ref object locker,saveBrowserError saveBrowserErrorDg, string tmpDir);

   
        /// <summary>
        /// Запуск браузера.
        /// </summary>
        bool startBrowser();

        /// <summary>
        ///Остановка браузера.
        /// </summary>
        void stopProcess();
        
    }
}
