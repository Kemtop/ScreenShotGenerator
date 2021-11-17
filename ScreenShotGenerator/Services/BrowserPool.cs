using ScreenShotGenerator.Services.BrowserControl;
using ScreenShotGenerator.Services.ScreenShoterPools;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Делегат для сохранения ошибок браузера.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="message"></param>
    public delegate void saveBrowserError(int level, string message, string url, string filename);

    /// <summary>
    /// Делегат для события появления новой задачи для браузеров.
    /// </summary>
    public delegate void hasNewJobForBrowsers();

    /// <summary>
    /// Пул браузеров.
    /// </summary>
    public class BrowserPool
    {
        //Пул объектов для управления браузерами.
        List<BrowserControlLogic> poolBrowserControls;

        /// <summary>
        /// Блокиратор пула браузеров.
        /// </summary>
        object lockerPool = new object();

        /// <summary>
        /// Событие появление новой работы для браузера.
        /// </summary>
        private event hasNewJobForBrowsers newJobForBrowser;

        /// <summary>
        /// Тайм аут загрузки страницы браузером.
        /// </summary>
        public int pageLoadTimeouts;
        /// <summary>
        /// Тайм аут загрузки скриптов браузером.
        /// </summary>
        public int javaScriptTimeouts;

        /// <summary>
        /// Делегат для записи ошибок браузера в БД.
        /// </summary>
        public saveBrowserError saveBrowserErrorDg;

        PoolTasks poolTask;

        /// <summary>
        /// Директория для хранения временных файлов.
        /// </summary>
        private String tmpDir;

        /// <summary>
        /// Уникальный идентификатор браузера.
        /// </summary>
        private int browserId;

        /// <summary>
        ///Содержимое страницы на которую браузер переходит перед созданием скрина. 
        /// </summary>
        string blankPage;

        /// <summary>
        /// Обработчик события по завершению выполнения задачи браузерами.
        /// </summary>
        BrowserEndJobOnPage OnBrowserTaskCompleted;

        /// <summary>
        /// Перезагружать браузер после определенного количество скриншотов. 0-не перезагружать.
        /// </summary>
        public int  browserRestartAfterScreens;

        

        public BrowserPool(String tmpDir, ref PoolTasks poolTask,BrowserEndJobOnPage OnBrowserTaskCompleted)
        {
            poolBrowserControls = new List<BrowserControlLogic>();
            //this.saveBrowserErrorDg = saveBrowserErrorDg;
            this.tmpDir = tmpDir;
            this.poolTask = poolTask;
            this.OnBrowserTaskCompleted = OnBrowserTaskCompleted;

        }

        /// <summary>
        /// Создает событие появления новой работы для браузеров.
        /// </summary>
        public void eventNewJobForBrowser()
        {           
            newJobForBrowser();
        }

        /// <summary>
        /// Считывает пустую страницу.
        /// </summary>
        /// <returns></returns>
        private string loadBlankPage()
        {
            string filePathFull = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot/blankPage.html");
            try
            {
                return File.ReadAllText(filePathFull);
            }
            catch(Exception ex)
            {
                Log.Error("Error read blank page:"+ex.Message);
                return null;
            }
            
        }


        /// <summary>
        /// Возвращает очередной идентификатор браузера.
        /// </summary>
        /// <returns></returns>
        private int getBrowserId()
        {
            if (browserId < Int32.MaxValue - 10)
                browserId++;
            else
                browserId = 1;

            return browserId;
        }


        /// <summary>
        /// Создает пул браузеров указанного размера.
        /// </summary>
        /// <param name="size"></param>
        public void createPool(int poolBrowserSize)
        {
            //Считываю страницу на которую браузер переходит перед созданием скрина. Говррю что это не url,а html строка.
             blankPage= "data:text/html;charset=utf-8,"+loadBlankPage();
            
            //Создаю пул браузеров.
            for (int i = 0; i < poolBrowserSize; i++)
            {
                int id = getBrowserId(); //Уникальный идентификатор браузера.

                Log.Information("Create browser " + id.ToString()); //Вывод информации.

                try
                {
                    //Запускает браузер и создает логику управления.
                    createItem(blankPage, id);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception in [createBrowserPool]:" + ex.Message);
                }
            }
        }

        /// <summary>
        /// Запускает браузер и создает логику управления.
        /// </summary>
        private void createItem(string blankPage,int browserId)
        {
            //Отладка.
            bool FireFox = true;
            //bool FireFox = false;
            BrowserControlLogic Bl = null; //Логика управления браузером.
            IBrowserControl Br = null; //Браузер.

            if (FireFox)
            {
                /*
                  Он тоже болен болезнью хрома.
               Bl = new BrowserControlLogic(
               new ImpBrowserControlEdge(pageLoadTimeouts, javaScriptTimeouts),//Задаю таймауты загрузки.
               saveBrowserErrorDg, tmpDir);
                */
                Br = new ImpBrowserControlFireFox(pageLoadTimeouts, javaScriptTimeouts);
            }
            else
            {
                //Задаю таймауты загрузки.
                Br = new ImpBrowserControlChrome(pageLoadTimeouts, javaScriptTimeouts, true, browserId);
                //Создаем экземпляр обьекта для управления браузером.
            }

            Br.blankPage = blankPage; //Страница перед созданием скриншота.
            Bl = new BrowserControlLogic(Br, saveBrowserErrorDg, tmpDir);
            Bl.tasksPerThread = 1; //Количество задач из пула которые браузер обрабатывает за раз.
            Bl.browserId = browserId; //Ид браузера, что бы потоки как то можно отличать.

            if (!Bl.startBrowser())//Запустить браузер. Выходим если не смог.
                return;

            //Назначаем обработчик завершения задачи.
            Bl.finishedJob += OnBrowserTaskCompleted;
            newJobForBrowser += Bl.OnNewJob; //Подписываем все браузеры на информирование о новой задаче.
            Bl.endLife += OnEndLifeBrowser; //Обрабатываем лимит срока работы браузера.

            //Перезагрузить браузер после лимита по количеству скринов.
            Bl.browserRestartAfterScreens = browserRestartAfterScreens;

            Bl.processPool(ref poolTask); //Запустить обработку пула задач.
            lock(lockerPool)
            {
                poolBrowserControls.Add(Bl);
            }
           
        }


        /// <summary>
        /// Очищаю пул браузеров. Закрываю все существующие.
        /// </summary>
        public void clearPool()
        {
            int i = 1;
            foreach (BrowserControlLogic bl in poolBrowserControls)
            {
                Log.Information("Close browser..." + i.ToString());
                bl.stopProcess();
                i++;
            }

            //Очищаю пулл.
            poolBrowserControls.Clear();
        }


        /// <summary>
        /// Обработчик события по окончанию времени жизни браузера.
        /// </summary>
        /// <param name="browserId"></param>
        private void OnEndLifeBrowser(int browserId)
        {
            Log.Information("Restart browser "+browserId);
            //Запускает новый браузер и создает логику управления.
            createItem(blankPage, getBrowserId());

            //Ищем браузер который нужно остановить.
            BrowserControlLogic Bl = poolBrowserControls.First(x=>x.browserId==browserId);
            Bl.shutdown();//Остановка браузера.
            Thread.Sleep(10000);
            lock(lockerPool)
            {
                poolBrowserControls.Remove(Bl);
            }
           
        }


        /// <summary>
        /// Возвращает количество работающих браузеров на текущий момент.
        /// </summary>
        /// <returns></returns>
        public int browserCount()
        {
            lock (lockerPool)
            {
               return poolBrowserControls.Count;
            }
        }

        /// <summary>
        /// Запускает новые браузеры в количестве.
        /// </summary>
        /// <param name="count"></param>
        public void startNewBrowser(int count)
        {
            //Создаю новые браузеры.
            for (int i = 0; i < count; i++)
            {
                int id = getBrowserId(); //Уникальный идентификатор браузера.

                Log.Information("Create browser " + id.ToString()); //Вывод информации.

                try
                {
                    //Запускает браузер и создает логику управления.
                    createItem(blankPage, id);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception in [createBrowserPool]:" + ex.Message);
                }
            }
        }

        /// <summary>
        /// Закрывает определенное количество браузеров и оставляем работать(count) указанное число.
        /// </summary>
        /// <param name="count"></param>
        public void leaveWorkBrowsers(int needWork)
        {
            lock (lockerPool)
            {
               int curentWork = poolBrowserControls.Count();
               if (needWork >= curentWork) return; //Не чего закрывать.Работает меньше чем требуется.

               int needClose = curentWork - needWork; //Нужно закрыть.
                                                   //Выбираем требуемое количество браузеров для закрытия.
                                                   //Закрываем первые, а не последние. Что бы автоматически подчищать.
                IEnumerable<BrowserControlLogic> BLtoClose = 
                    poolBrowserControls.OrderBy(x=>x.browserId).Take(needClose);
                //Отправляем всем сигнал завершения работы.
                foreach (BrowserControlLogic B in BLtoClose)
                {
                    B.shutdown();
                    poolBrowserControls.Remove(B); //Очистить пул.
                }
                   
            }

        }

    }
}
