
using ScreenShotGenerator.Services.ScreenShoterLogic;
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
        /// Обработка задач в потоке задач. Выполняется бесконечно.
        /// Управляющий процесс запускает в отдельном потоке.
        /// </summary>
        /// <param name="poolTasks"></param>
        void processPool(ref poolTasks pool,ref object locker);

        /// <summary>
        /// Запуск браузера.
        /// </summary>
        void startBrowser();

        /// <summary>
        ///Остановка браузера.
        /// </summary>
        void stopBrowser();
        
    }
}
