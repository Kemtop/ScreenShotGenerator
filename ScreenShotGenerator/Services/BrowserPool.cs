using ScreenShotGenerator.Services.BrowserControl;
using ScreenShotGenerator.Services.ScreenShoterPools;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        //Синхронизация потоков, для работы с общим пулом.
        object lockPoolTask;
        poolTasks poolTask;

        /// <summary>
        /// Директория для хранения временных файлов.
        /// </summary>
        private String tmpDir;

        /// <summary>
        /// Обработчик события по завершению выполнения задачи браузерами.
        /// </summary>
        BrowserEndJobOnPage OnBrowserTaskCompleted;


        public BrowserPool(String tmpDir, ref poolTasks poolTask,
            ref object lockPoolTask, BrowserEndJobOnPage OnBrowserTaskCompleted)
        {
            poolBrowserControls = new List<BrowserControlLogic>();
            //this.saveBrowserErrorDg = saveBrowserErrorDg;
            this.tmpDir = tmpDir;


            //Синхронизация потоков, для работы с общим пулом.
            this.lockPoolTask = lockPoolTask;
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
        /// Создает пул браузеров указанного размера.
        /// </summary>
        /// <param name="size"></param>
        public void createPool(int poolBrowserSize)
        {
            //Считываю страницу на которую браузер переходит перед созданием скрина. Говррю что это не url,а html строка.
            string blankPage= "data:text/html;charset=utf-8,"+loadBlankPage();
            
            //Создаю пул браузеров.
            for (int i = 0; i < poolBrowserSize; i++)
            {
                Log.Information("Create browser " + (i + 1).ToString()); //Вывод информации.

                try
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
                        Br = new ImpBrowserControlChrome(pageLoadTimeouts, javaScriptTimeouts, true, i + 1);
                                              //Создаем экземпляр обьекта для управления браузером.
                    }

                    Br.blankPage = blankPage; //Страница перед созданием скриншота.
                    Bl = new BrowserControlLogic(Br, saveBrowserErrorDg, tmpDir);
                    Bl.tasksPerThread = 1; //Количество задач из пула которые браузер обрабатывает за раз.
                    Bl.browserId = i + 1; //Ид браузера, что бы потоки как то можно отличать.

                    if (!Bl.startBrowser())//Запустить браузер. Выходим если не смог.
                        break;

                    //Назначаем обработчик завершения задачи.
                    Bl.finishedJob += OnBrowserTaskCompleted;
                    newJobForBrowser += Bl.OnNewJob; //Подписываем все браузеры на информирование о новой задаче.

                    Bl.processPool(ref poolTask, ref lockPoolTask); //Запустить обработку пула задач.
                    poolBrowserControls.Add(Bl);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception in [createBrowserPool]:" + ex.Message);
                }
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
    }
}
