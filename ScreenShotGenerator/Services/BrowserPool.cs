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
        /// Монитор свопа.
        /// </summary>
        private SwapMonitor swapMonitor;

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

        /// <summary>
        /// Информирует ждущие службы об остановке сервиса.
        /// </summary>
        private bool serviceStoping;
                
        public BrowserPool(String tmpDir, ref PoolTasks poolTask,BrowserEndJobOnPage OnBrowserTaskCompleted)
        {
            poolBrowserControls = new List<BrowserControlLogic>();
            this.tmpDir = tmpDir;
            this.poolTask = poolTask;
            this.OnBrowserTaskCompleted = OnBrowserTaskCompleted;
            swapMonitor = new SwapMonitor();
            swapMonitor.SaveCurentPids(); //Сохраняю данные о текущих процессах.
        }

        /// <summary>
        /// Создает событие появления новой работы для браузеров.
        /// </summary>
        public void eventNewJobForBrowser()
        {           
            newJobForBrowser();
        }

        
        /// <summary>
        /// Создает пул браузеров указанного размера.
        /// </summary>
        /// <param name="size"></param>
        public void createPool(int poolBrowserSize)
        {
            Log.Information("Running browser control service...");

            //Считываю страницу на которую браузер переходит перед созданием скрина. Говррю что это не url,а html строка.
            //blankPage= "data:text/html;charset=utf-8,"+loadBlankPage();
            blankPage = "about:blank";

            //Создаю пул браузеров.
            for (int i = 0; i < poolBrowserSize; i++)
            {
                int id = BrowserIdGenerator.getId(); //Уникальный идентификатор браузера.

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

            swapMonitor.eventSwapLimit+=OnEndLifeBrowser; //Если превышен лимит swap.
            swapMonitor.runMonitoring();//Запускаю мониторинг использования браузерами свопа.
            Log.Information("Browser control service it running.");
        }

        /// <summary>
        /// Очищаю пул браузеров. Закрываю все существующие.
        /// </summary>
        public void clearPool()
        {
            Log.Information("Stoping browsers in pool...");

            int i = 1;
            foreach (BrowserControlLogic bl in poolBrowserControls)
            {
                Log.Information("Close browser..." + i.ToString());
                bl.stopProcess();
                i++;
            }

            //Очищаю пулл.
            poolBrowserControls.Clear();
            Log.Information("Browsers pool clear. All browser stoped.");
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
            catch (Exception ex)
            {
                Log.Error("Error read blank page:" + ex.Message);
                return null;
            }

        }


        /// <summary>
        /// Запускает браузер и создает логику управления.
        /// </summary>
        private void createItem(string blankPage,int id)
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
                Br = new ImpBrowserControlChrome(pageLoadTimeouts, javaScriptTimeouts, true, id);
                //Создаем экземпляр обьекта для управления браузером.
            }

            Br.blankPage = blankPage; //Страница перед созданием скриншота.
            Bl = new BrowserControlLogic(Br, saveBrowserErrorDg, tmpDir);
            Bl.tasksPerThread = 1; //Количество задач из пула которые браузер обрабатывает за раз.
            Bl.browserId = id; //Ид браузера, что бы потоки как то можно отличать.

            if (!Bl.startBrowser())//Запустить браузер. Выходим если не смог.
                return;

            //Назначаем обработчик завершения задачи.
            Bl.finishedJob += OnBrowserTaskCompleted;
            newJobForBrowser += Bl.OnNewJob; //Подписываем все браузеры на информирование о новой задаче.
            Bl.endLife += OnEndLifeBrowser; //Обрабатываем лимит срока работы браузера.
            Bl.eventBrowserDie += OnBrowserDie; //Событие по внезапному выходу из строя.
            Bl.eventClosed +=OnBrowserClose;

            //Перезагрузить браузер после лимита по количеству скринов.
            Bl.browserRestartAfterScreens = browserRestartAfterScreens;

            //Считывает и сохраняет PID процессов драйвера.
            int cnt = 0;
            while (!serviceStoping && (cnt < 10))
            {
                if (swapMonitor.getDriverPids(id)) break; //Если успешно получили.
                cnt++;
                Thread.Sleep(1000);
            }
            
            if(cnt>10)
            {
                Log.Error("Fatal error! Can't get pid info for browser.");
                throw new Exception("Fatal error!");
            }

            Bl.processPool(ref poolTask); //Запустить обработку пула задач.
            lock(lockerPool)
            {
                poolBrowserControls.Add(Bl);
            }
           
        }

             


        /// <summary>
        /// Обработчик события по окончанию времени жизни браузера.
        /// </summary>
        /// <param name="browserId"></param>
        private void OnEndLifeBrowser(int id)
        {
            //Существует ли браузер который нужно перезапустить.
            //Ищем браузер который нужно остановить.
            BrowserControlLogic Bl = poolBrowserControls.FirstOrDefault(x => x.browserId == id);
            if ((Bl!=null)&&(Bl.beginShutdown)) //Браузер не закрыт и уже была отправлена команда закрытия.
            {
                Log.Information("Browser " + id.ToString()+" in closing process.");
                return;
            }
            
            Log.Information("Browser "+id+" broken. Run new.");
            //Запускает новый браузер и создает логику управления.
            createItem(blankPage, BrowserIdGenerator.getId());

            Log.Information("Call shutdown for broken browser(" + id + ").");
            if (Bl == null)
                Log.Error("Not found browser(" + id.ToString() + " in pool.");
            else
            Bl.shutdown();//Остановка браузера.                    
        }

        /// <summary>
        /// Обработчик события закрытия браузера.
        /// </summary>
        /// <param name="id"></param>
        private void OnBrowserClose(int id)
        {
            if (serviceStoping) return; //Получена команда остановки сервиса. Ни как не реагируем на закрытие браузеров.

            //Проверить не работают ли процессы браузера.
            while (!swapMonitor.hasAnyProcess(id))
            {
                Log.Information("Browser(" + id.ToString() + ") process steel work!");
                Thread.Sleep(10000);
            }

            Log.Information("Browser(" + id.ToString() + ") processes stoping. Remove from browser pool.");
            BrowserControlLogic Bl = poolBrowserControls.FirstOrDefault(x => x.browserId == id);
            if (Bl != null) //Браузер cуществует.
            {
                lock (lockerPool)
                {
                    poolBrowserControls.Remove(Bl);
                }
                swapMonitor.removePid(id); //Удаляю информацию о процессах данного браузера.   
            }
            else
                Log.Information("Browser(" + id.ToString() + ") not found in browser pool.");

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
                int id = BrowserIdGenerator.getId(); //Уникальный идентификатор браузера.

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

        /// <summary>
        /// Обработчик события выхода из строя браузера.
        /// </summary>
        /// <param name="browserId"></param>
        private void OnBrowserDie(int id)
        {
            lock (lockerPool)
            {
                //Ищем не рабочий браузер.
                BrowserControlLogic Bl = poolBrowserControls.First(x => x.browserId == id);
                Bl.eventBrowserDie -= OnBrowserDie;
                poolBrowserControls.Remove(Bl); //Очистить пул.
            }
        }

        /// <summary>
        ///Закрываю браузеры и информирую об остановке сервиса всю вложенную логику.
        /// </summary>
        public void Stop()
        {
            serviceStoping = true;
            clearPool(); //Закрываю все браузеры.
        }
    }
}
