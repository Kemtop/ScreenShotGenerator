﻿using ScreenShotGenerator.Services.BrowserControl;
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
        public int browserRestartAfterScreens;

        /// <summary>
        /// Информирует ждущие службы об остановке сервиса.
        /// </summary>
        private bool serviceStoping;

        public BrowserPool(String tmpDir, ref PoolTasks poolTask, BrowserEndJobOnPage OnBrowserTaskCompleted)
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

            swapMonitor.eventSwapLimit += OnSwapLimit; //Если превышен лимит swap.
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
                //Количество живых браузеров.
                return poolBrowserControls.Where(x=>x.beginShutdown==false).Count();
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
        private void createItem(string blankPage, int id)
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
            Bl.eventClosed += OnBrowserClose;

            //Перезагрузить браузер после лимита по количеству скринов.
            Bl.browserRestartAfterScreens = browserRestartAfterScreens;

            //Считывает и сохраняет PID процессов драйвера.
            if (!swapMonitor.getDriverPids(id))//Ошибка получения,если systemctl вернет что то странное.
            {
                Log.Error("Fatal error! Can't get pid info for browser.");
                throw new Exception("Fatal error!");
            }


            Bl.processPool(ref poolTask); //Запустить обработку пула задач.
            lock (lockerPool)
            {
                poolBrowserControls.Add(Bl);
            }

            //Сообщаю браузеру о новых задачах.Это нужно если происходит перезапуск браузера.
            Bl.OnNewJob();//Что бы новый браузер начал обрабатывать задачу. 
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
            if ((Bl != null) && (Bl.beginShutdown)) //Браузер не закрыт и уже была отправлена команда закрытия.
            {
                KillNoClosedBrowser(Bl); //Убивает браузер который не захотел закрыться.
                return;
            }

            Log.Information("Browser " + id + " broken. Run new.");
            //Запускает новый браузер и создает логику управления.
            createItem(blankPage, BrowserIdGenerator.getId());

            Log.Information("Call shutdown for broken browser(" + id + ").");
            if (Bl == null)
                Log.Error("Not found browser(" + id.ToString() + " in pool.");
            else
                Bl.shutdown();//Остановка браузера.                    

            //Разблокировка логики увеличения уменьшения количества браузеров в зависимости от нагрузки.
            TimeLimitManipulationAllow.UnlockBrowserManagment();
        }


        /// <summary>
        /// Обработчик события по окончанию времени жизни браузера.
        /// </summary>
        /// <param name="browserId"></param>
        private void OnSwapLimit(int id,int size)
        {
            //Блокировка создания новых браузеров и отключение старых системой регулировки нагрузки.
            TimeLimitManipulationAllow.LockBrowserManagment(); //swap monitor блокирует работу анализатору нагрузки.

            //Нет критического переполнения свопа.
            if (size<100000)
            {
                OnEndLifeBrowser(id); //Обычная остановка браузера.
                RemoveStopedBrowsersInPool();// Удаляет остановленные браузеры из пула.
                return;
            }

            //Существует ли браузер для которого критическое переполние.
            BrowserControlLogic Bl = poolBrowserControls.FirstOrDefault(x => x.browserId == id);
            if (Bl == null)
            {
                Log.Error("Not found browser(" + id.ToString() + ") for critical stop!");
                return;
            }

            if (Bl.beginShutdown) //Браузер не закрыт и уже была отправлена команда закрытия.
            {
                KillNoClosedBrowser(Bl); //Убивает браузер который не захотел закрыться.
                return;
            }

            Bl.CriticalStop(); //Критическая остановка.
            //Критическая остановка браузера. Что бы система не упала от резкого роста swap(за 30сек 2Гб).
            Log.Information("Critical stop for browser("+id.ToString()+").Size="+size.ToString());            
            
            Thread.Sleep(1000); //Ожидание очистки swap, что бы система не упала.
            swapMonitor.killBrowserProcesses(id); //Однозначное удаление всех процессов браузера.
           
            Log.Information("Browser " + id + " broken. Run new.");
            //Запускает новый браузер и создает логику управления.
            createItem(blankPage, BrowserIdGenerator.getId());

            //Разблокировка логики увеличения уменьшения количества браузеров в зависимости от нагрузки.
            TimeLimitManipulationAllow.UnlockBrowserManagment();
            RemoveStopedBrowsersInPool();// Удаляет остановленные браузеры из пула.
        }

        /// <summary>
        /// Убивает браузер который не захотел закрыться, и запускает процесс разблокировки логики
        /// увеличения уменьшения количества браузеров в зависимости от нагрузки.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private void KillNoClosedBrowser(BrowserControlLogic Bl)
        {
            Log.Information("Browser " +Bl.browserId.ToString() + " not closed. Try new shutdown and kill.");
            Bl.shutdown();
            swapMonitor.killBrowserProcesses(Bl.browserId); //Однозначное удаление всех процессов браузера.
            //Разблокировка логики увеличения уменьшения количества браузеров в зависимости от нагрузки.
            TimeLimitManipulationAllow.UnlockBrowserManagment();
        }


        /// <summary>
        /// Обработчик события закрытия браузера.
        /// </summary>
        /// <param name="id"></param>
        private void OnBrowserClose(int id)
        {
            if (serviceStoping) return; //Получена команда остановки сервиса. Ни как не реагируем на закрытие браузеров.

            Log.Information("OnBrowserClose");
            /*
            //Проверить не работают ли процессы браузера.
            while (!swapMonitor.hasAnyProcess(id))
            {
                Log.Information("Browser(" + id.ToString() + ") process steel work!");
                Thread.Sleep(10000);
            }
            */
            //Удалять данные браузера нельзя, так как могут остаться какие то процессы!
            /*
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
            */
        }

        /// <summary>
        /// Запускает новые браузеры в количестве.
        /// </summary>
        /// <param name="count"></param>
        public void startNewBrowser(int count)
        {
            //swap monitor заблокировал управление.
            //Можно ли изменять количество браузеров. Не делали ли это только что?
            if (!TimeLimitManipulationAllow.WeCanManipulationBrowsersAmount()) return;

            Log.Information("startNewBrowser by request logic " + count.ToString()+" items.");

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
            //swap monitor заблокировал управление.
            //Можно ли изменять количество браузеров. Не делали ли это только что?
            if (!TimeLimitManipulationAllow.WeCanManipulationBrowsersAmount()) return;

            lock (lockerPool)
            {
                //Выбираю только рабочие браузеры.
                int curentWork = poolBrowserControls.Where(x=>x.beginShutdown==false).Count();
                if (needWork >= curentWork) return; //Не чего закрывать.Работает меньше чем требуется.

                Log.Information("Now work "+curentWork.ToString()+" browsers.");

                int needClose = curentWork - needWork; //Нужно закрыть.
                                                       //Выбираем требуемое количество браузеров для закрытия.
                                                       //Закрываем первые, а не последние. Что бы автоматически подчищать.
                //Только те которые нормально работают.
                IEnumerable<BrowserControlLogic> BLtoClose =
                    poolBrowserControls.Where(x=>x.beginShutdown==false).OrderBy(x => x.browserId).Take(needClose);

                //Отправляем всем сигнал завершения работы.
                foreach (BrowserControlLogic B in BLtoClose)
                {
                    B.shutdown();
                    Log.Information("leaveWorkBrowser by request logic " + B.browserId.ToString());
                    poolBrowserControls.Remove(B); //Очистить пул.                   
                }
                Log.Information("Closed by request logic " + BLtoClose.Count().ToString()+" browsers.");
            }

        }



        /// <summary>
        /// Обработчик события выхода из строя браузера.
        /// </summary>
        /// <param name="browserId"></param>
        private void OnBrowserDie(int id)
        {
            //Блокировка создания новых браузеров и отключение старых системой регулировки нагрузки.
            TimeLimitManipulationAllow.LockBrowserManagment(); //swap monitor блокирует работу анализатору нагрузки.

            lock (lockerPool)
            {
                //Ищем не рабочий браузер.
                BrowserControlLogic Bl = poolBrowserControls.First(x => x.browserId == id);
                Bl.eventBrowserDie -= OnBrowserDie;
            }

            Log.Information("Browser(" + id.ToString() + ") Die. Kill him processes.");
            swapMonitor.killBrowserProcesses(id); //Однозначное удаление всех процессов браузера.

            Log.Information("Run new.");
            //Запускает новый браузер и создает логику управления.
            createItem(blankPage, BrowserIdGenerator.getId());

            //Разблокировка логики увеличения уменьшения количества браузеров в зависимости от нагрузки.
            TimeLimitManipulationAllow.UnlockBrowserManagment();
        }

        /// <summary>
        ///Закрываю браузеры и информирую об остановке сервиса всю вложенную логику.
        /// </summary>
        public void Stop()
        {
            serviceStoping = true;
            clearPool(); //Закрываю все браузеры.
        }


        /// <summary>
        /// Удаляет остановленные браузеры из пула.
        /// </summary>
        private void RemoveStopedBrowsersInPool()
        {
            try
            {
                DateTime now = DateTime.Now;
                lock (lockerPool)
                {
                    //Браузер остановлен час назад.
                    IEnumerable<BrowserControlLogic> BList = poolBrowserControls.
                     Where(x => x.beginShutdown == true && (now-x.EndLifeTime).TotalHours>1.0).ToList();

                    foreach (BrowserControlLogic Bl in BList)
                    {
                        swapMonitor.removePid(Bl.browserId); //Удаляю информацию о процессах данного браузера.   
                        poolBrowserControls.Remove(Bl);
                        Log.Information("Browser(" + Bl.browserId.ToString() + ") remove from pool.");
                    }
                }
            }
            catch(Exception ex)
            {
                Log.Error("Exception RemoveStopedBrowsersInPool "+ex.Message);
            }
           
        }

    }
}
