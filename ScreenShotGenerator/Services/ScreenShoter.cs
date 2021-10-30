using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using ScreenShotGenerator.Services.BrowserControl;
using ScreenShotGenerator.Services.ScreenShoterLogic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using ScreenShotGenerator.Models;
using ScreenShotGenerator.Data;
using Microsoft.Extensions.DependencyInjection;
using ScreenShotGenerator.Entities;
using Microsoft.Extensions.Configuration;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Делегат для сохранения ошибок браузера.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="message"></param>
     public delegate void saveBrowserError(int level,string message,string url,string filename);

    public class ScreenShoter : IScreenShoter
    {
        private readonly IHttpContextAccessor _context;
        private readonly IServiceScopeFactory scopeFactory;

        //Синхронизация потоков, для работы с общим пулом.
        static object locker = new object();

        //Блокировка кеши poolCache.
        static object lockCachePool = new object();


        //Директория для хранения временных файлов.
        const String tmpDir = "imgCache";

        //Полное имя хоста.
        private String hostName = null;


        //Пул задач.
        poolTasks poolTask;
        int elementId = 0;//Ид элементов в списке. Идентификатор элемента для возможности его сортировки по возрастанию.


        //Кешь уже созданых скриншотов. Хранит сведения о них. Заполняется в процессе работы сервиса.
        poolTasks poolCache;


        //Пул объектов для управления браузерами.
        List<IBrowserControl> poolBrowserControls;
        //int poolBrowserSize = 4; //Количество запущенных браузеров.
        //int browserTasksPerThread=5; //Количество задач из пула которые браузер обрабатывает за раз.

        int poolBrowserSize = 1; //Количество запущенных браузеров.
        int browserTasksPerThread = 5; //Количество задач из пула которые браузер обрабатывает за раз.
        int clearCashInterval = 10; //Интервал очистки кеша, в часах.

        /// <summary>
        /// Включает чтение кеши из базы данных при запуске сервиса.
        /// Используется для отладки приложения.
        /// </summary>
        bool enableReadCacheFromDbInStart = true;

        /// <summary>
        /// Таймер очистки выполненных задача в пуле задач.
        /// </summary>
        Timer timerClearComplatePoolTasks;

        /// <summary>
        /// Таймер запускающий задачу проверки необходимости очистки кеша.
        /// </summary>
        Timer timerClearCache;

        /// <summary>
        /// Флаг сообщающий о начале процесса очистки пула задач. Запрещает добавление новых, пока не закончиться
        /// процесс очистки.
        /// </summary>
        private bool runClearPoolTasks;


        CancellationToken _cancellationToken;

        //Тестовое, удали.
        public int timeGo;

        /// <summary>
        /// Делегат для записи ошибок браузера в БД.
        /// </summary>
        saveBrowserError saveBrowserErrorDg;


        public ScreenShoter(
            IHttpContextAccessor context,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            Action<ScreenShoter> action)
        {
            _context = context;
            this.scopeFactory = scopeFactory;

            poolTask = new poolTasks();
            poolTask.tmpDir = tmpDir; //Директория для хранения скриншотов.

            poolCache = new poolTasks();
            poolBrowserControls = new List<IBrowserControl>();

            //Чтение настроек сервиса.
            readSettingsFromDb();

            //Задаю настройки переодическим действиям. Тест можно удалить.
            action(this);


            if (configuration["ScreenShoter:enableReadCacheFromDbInStart"] == "false")
            {
                enableReadCacheFromDbInStart = false;
            }

            //В минутах.
            int interval1 = Convert.ToInt32(configuration["ScreenShoter:ClearComplatePoolTasks"]);
            interval1*= 60000; //Переводим в минуты.
            timerClearComplatePoolTasks = new Timer((Object stateInfo) =>
            {
                ClearPoolTasks();
            }, null, interval1, interval1);


            //Таймер запускающий задачу проверки необходимости очистки кеша.
            int interval2 = Convert.ToInt32(configuration["ScreenShoter:intervalCheckNeedClearCash"]);
            timerClearCache = new Timer((Object stateInfo) =>
            {
               // clearCache();
            }, null, 1000, interval2 * 60000);

            //Делегат для записи ошибок.
            saveBrowserErrorDg = saveBrowserError;

        }

        /// <summary>
        /// Возвращает настройки сервиса для отображения на админ панеле.
        /// </summary>
        /// <returns></returns>
        public SystemSettingModel getSettings()
        {
            SystemSettingModel m = new SystemSettingModel();
            m.browserAmount = poolBrowserSize;  //Количество запущенных браузеров.
            m.tasksAmount = browserTasksPerThread; //Количество задач из пула которые браузер обрабатывает за раз.
            m.clearCashInterval = clearCashInterval; //Интервал очистки кеша, в часах.
            m.cacheElementsCnt = poolCache.cacheCnt();

            //Количество элементов обрабатываемых на данный момент.
            m.curentElementsInProcessCnt = poolTask.curentElementsInProcessCnt();

            return m;
        }

        /// <summary>
        /// Запускает сервис.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async void runService(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            await Task.Delay(2000);
            runTasks(); //Создает пул браузеров.

            //Считывает данные кеш из базы данных в память(объект poolCash).
            if (enableReadCacheFromDbInStart)
                readFromDbToCash();
        }

        /// <summary>
        /// Создает пул браузеров.
        /// </summary>
        private void runTasks()
        {
            Log.Information("Running browser control service...");
            createBrowserPool();//Создаем пул браузеров.
            Log.Information("Browser control service it running.");

        }


        /// <summary>
        /// Перезапускает службу.
        /// </summary>
        public async void restartService()
        {
            stopBrowserPool();
            //Останавливает все задачи в пуле, закрывает браузеры.
            int delay = 10000;
            Log.Information("Waiting " + (delay / 1000).ToString() + "s"); ;

            await Task.Delay(delay);
            runTasks();
            Log.Information("Service is restarted.");
        }


        /// <summary>
        /// Останавливает все задачи в пуле, закрывает браузеры.
        /// </summary>
        private void stopBrowserPool()
        {
            Log.Information("Stoping services...");

            int i = 1;
            foreach (IBrowserControl bc in poolBrowserControls)
            {
                Log.Information("Close browser..." + i.ToString());
                bc.stopProcess();
                i++;
            }

            Log.Information("StopService.");
        }

        public Task stopService(CancellationToken cancellationToken)
        {
            stopBrowserPool();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Создаю пул браузеров.
        /// </summary>
        private void createBrowserPool()
        {
            //Создаю пул браузеров.
            for (int i = 0; i < poolBrowserSize; i++)
            {
                Log.Information("Create browser " + (i + 1).ToString()); //Вывод информации.

                try
                {
                    IBrowserControl Bc = new ImpBrowserControlChrome();
                    Bc.tasksPerThread = browserTasksPerThread; //Количество задач из пула которые браузер обрабатывает за раз.
                    Bc.setTaskId(i + 1); //Ид браузера, что бы потоки как то можно отличать.
                    Bc.startBrowser();//Запустить браузер.
                                      //Пока не понятно нужна ли тут задержка.
                    Bc.processPool(ref poolTask, ref locker, saveBrowserErrorDg); //Запустить обработку пула задач.
                    poolBrowserControls.Add(Bc);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception in [createBrowserPool]:" + ex.Message);
                }
            }

        }


        /// <summary>
        /// Добавляет задачу в пул задач.
        /// </summary>
        /// <param name="t"></param>
        private void addTask(mJobPool t)
        {
            //Если начат процесс очистки пула, ждем завершения.
            while (runClearPoolTasks)
            {
                Task.Delay(1000);
                if (_cancellationToken.IsCancellationRequested) return;
            }

            poolTask.add(t);
        }



        /// <summary>
        /// Добавляет задачу в пул задач и ждет ее завершения.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public List<mUserJson> runJob(string[] urls, string userIP)
        {
            //Получение корневого url запроса, если уже его не получили.
            getHostName();

            //Список для быстрого мониторинга отправленных задач.
            //Так как содержит ссылки на задачи.
            List<mJobPool> jb = new List<mJobPool>();

            //Прохожу по списку урл.          
            foreach (string url in urls)
            {
                mJobPool t; //Задача.

                //Поиск выполненной задачи в кеш сервиса.
                mJobPool cashValue = poolCache.findUrl(url);

                if (cashValue == null) //Задача не была выполнена,добавляю в пул.
                {
                    t = new mJobPool(); //Новая задача.
                    t.url = url;
                    t.id = elementId; //Идентификатор элемента для возможности его сортировки.

                    //Исключение ошибки переполнения. -10 просто так.
                    if (elementId == int.MaxValue - 10)
                        elementId = 0;//Обнуляю.
                    else
                        elementId++;

                    addTask(t);//Добавляю задачу в пулл задач.

                }
                else //Нашел выполненную.
                {
                    t = cashValue;
                }

                jb.Add(t);
            }


            int taskCnt = jb.Count; //Количество задач.

            int cntComplate = 0; //Количество выполненных задач.

            //Жду пока браузеры не обработают задачи. Или не остановят процесс.
            while (!_cancellationToken.IsCancellationRequested)
            {
                //Считаю количество выполненных задач или задач с ошибками.
                cntComplate = 0;
                foreach (mJobPool t in jb)
                {
                    if ((t.status == 3) || (t.status == 2)) cntComplate++;
                }


                //Все задачи выполнены.
                if (taskCnt == cntComplate)
                {
                    //Добавляет сведения о выполенных задачах в кеш.
                    addToCash(ref jb);
                    logErrors(ref jb, userIP);//Сохраняю сведени об ошибках.

                    //Преобразовываю в пользовательский json.
                    return createUserJson(ref jb);

                }

            }

            return null;

        }


        /// <summary>
        /// Получение корневого url, если уже его не получили.
        /// </summary>
        private void getHostName()
        {
            if (hostName == null)
            {
                var request = _context.HttpContext.Request;
                hostName = request.Scheme + "://" + request.Host.Value;
                poolTask.hostName = hostName; //Передаю значение другим потокам.
                poolTask.tmpDir = tmpDir;//Директория в которой храняться картинки.
            }

        }


        /// <summary>
        /// Преобразовываю в пользовательский json.
        /// </summary>
        /// <param name="jb"></param>
        /// <returns></returns>
        private List<mUserJson> createUserJson(ref List<mJobPool> jb)
        {
            List<mUserJson> userList = new List<mUserJson>();

            foreach (mJobPool j in jb)
            {
                mUserJson userLine = new mUserJson();
                userLine.url = j.url;


                //Если не было ошибок.
                if (j.status == 3)
                {
                    String fullname = hostName + "/" + tmpDir + "/" + j.fileName;
                    userLine.path = fullname;
                    userLine.status = 1;
                }

                //Возникла ошибка.
                if (j.status == 2)
                {
                    userLine.log = j.path;
                    userLine.status = 0;
                }

                userList.Add(userLine);
            }

            return userList;
        }



        /// <summary>
        /// Добавляет данные в кеш. Если ресурс занят-ждет.
        /// </summary>
        /// <param name="t"></param>
        private void waitAddToCachPool(mJobPool j)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                lock (lockCachePool)
                {
                    poolCache.add(j);
                    break;
                }

                //Если ресурс заблокирован, ждем.
                Task.Delay(1000);
            }

        }

        /// <summary>
        /// Добавляет сведения о выполенных задачах в кеш.
        /// </summary>
        private void addToCash(ref List<mJobPool> jb)
        {

            bool needSaveDb = false; //Необходимо обновить данные в таблице.

            //Добавляю строку в БД.
            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
              
                foreach (mJobPool j in jb)
                {
                    //Объект еще не находиться в кеши и обработан успешно.
                    if ((j.inCash == false) && (j.status == 3))
                    {
                        //Что бы не было явных пересечений при обработки одинаковых урл.
                        j.inCash = true; //Говорим что объект кеширован.

                        waitAddToCachPool(j); //Добавляю в кеш, если не заблокирован. Или ждем.                                        

                        //Cохраняю в БД на случай перезагрузки сервера.
                        mCashTable line = new mCashTable();
                        line.url = j.url;
                        line.timestamp = j.timestamp;
                        line.fileName = j.fileName;

                        db.screnshotCache.Add(line);
                        needSaveDb = true;
                    }
                }

                //Необходимо сохранить значения.
                if (needSaveDb)
                {
                  db.SaveChanges();                  
                }
            }
            
        }


        /// <summary>
        /// Считывает данные кеш из базы данных в память(объект poolCash).
        /// </summary>
        private void readFromDbToCash()
        {
            //Пулучаю данные из таблицы в виде списка.          
            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                List<mCashTable> table;
                table = db.screnshotCache.ToList();

                foreach (mCashTable ct in table)
                {
                    mJobPool j = new mJobPool();
                    j.fileName = ct.fileName;
                    j.timestamp = ct.timestamp;
                    j.url = ct.url;
                    j.status = 3; //Задача уже выполнена.
                    j.inCash = true; //Объект в кеши.
                    poolCache.add(j);
                }
            }
        }


        /// <summary>
        /// Логирую ошибки полученные при выполнении запроса.
        /// </summary>
        /// <param name="jb"></param>
        private void logErrors(ref List<mJobPool> jb, string userIP)
        {
            foreach (mJobPool j in jb)
            {
                //Выбираем только ошибки.
                if (j.status == 2)
                {
                    string str = userIP + ";" + j.url + ";" + j.path;
                    Log.Error(str);
                }

            }
        }

        /// <summary>
        /// Возвращает список имен параметров настройки сервиса.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> returnSettingsName()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            //Названия настроек.
            settings.Add("poolBrowserSize", "");
            settings.Add("browserTasksPerThread", "");
            settings.Add("clearCashInterval", "");

            return settings;
        }

        /// <summary>
        /// Чтение настроек сервиса.
        /// </summary>
        private void readSettingsFromDb()
        {
            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                //Возвращает список имен параметров настройки сервиса.
                Dictionary<string, string> settings = returnSettingsName();

                foreach (KeyValuePair<string, string> item in settings)
                {
                    settings[item.Key] = dbContext.serviceSettings.Where(x => x.Name == item.Key).
                         FirstOrDefault().Value;
                }

                poolBrowserSize = Convert.ToInt32(settings["poolBrowserSize"]);  //Количество запущенных браузеров.
                                                                                 //Количество задач из пула которые браузер обрабатывает за раз.
                browserTasksPerThread = Convert.ToInt32(settings["browserTasksPerThread"]);
                //Интервал очистки кеша, в часах.
                clearCashInterval = Convert.ToInt32(settings["clearCashInterval"]);
            }

        }

        /// <summary>
        /// Сохраняет настройки в БД.
        /// </summary>
        /// <param name="m"></param>
        public void setSettings(SystemSettingModel m)
        {
            //Возвращает список имен параметров настройки сервиса.
            Dictionary<string, string> settings = returnSettingsName();

            //Передаю  новые значения.
            settings["poolBrowserSize"] = m.browserAmount.ToString();  //Количество запущенных браузеров.
            settings["browserTasksPerThread"] = m.tasksAmount.ToString(); //Количество задач из пула которые браузер обрабатывает за раз.
            settings["clearCashInterval"] = m.clearCashInterval.ToString(); //Интервал очистки кеша, в часах.

            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                foreach (KeyValuePair<string, string> item in settings)
                {
                    //Получаю объект с нужным ключем.
                    mServiceSettings line = dbContext.serviceSettings.Where(x => x.Name == item.Key).
                         FirstOrDefault();
                    line.Value = settings[item.Key];
                }

                dbContext.SaveChanges();
            }
        }

        /// <summary>
        /// Возвращает количество ожидающих задач в пуле задач.
        /// </summary>
        /// <returns></returns>
        public int getWaitTasksCnt()
        {
            return poolTask.waitTasksCnt();
        }


        /// <summary>
        /// Удаляет выполненные задачи из пула.
        /// </summary>
        private void ClearPoolTasks()
        {
            //Жду пока разблокируют пулЗадач.
            while (!_cancellationToken.IsCancellationRequested)
            {
                //Запрещает другим потокам работать с пулом на время его очистки.
                lock (locker)
                {
                    //Говорю что я начал процесс очистки пула, и добавлять новые задачи в него не нужно.
                    //Иначе будут ошибки. 
                    runClearPoolTasks = true;

                    // Удалить завершенные задачи и задачи с ошибками.
                    // Возвращает количество удаленных.
                    int cnt = poolTask.clearComplate();
                    Log.Information("Clear pool Task comlete. Clear=" + cnt.ToString());

                    runClearPoolTasks = false;

                    return;
                }

                Task.Delay(500);
            }

        }


        /// <summary>
        /// Очищает кеш в памяти и БД.
        /// </summary>
        private void clearCache()
        {
            //clearCashInterval в часах.
            Log.Information("Check cache to need clear.");

            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                //Выбираю те у которых истек интервал хранения в кеши.
                //clearTbCnt  

                int clearTbCnt = 0;//Количество очищенных объектов в таблице. 
                List<mCashTable> tb = dbContext.screnshotCache.Where(x =>
             x.timestamp.AddDays(clearCashInterval) > DateTime.Now).ToList();
                clearTbCnt=tb.Count();

                //Удаление файлов на диске.
                Log.Information("Delete from disk."+clearTbCnt.ToString()+" items.");

                foreach (mCashTable t in tb)
                {
                    try
                    {
                        var path = Path.Combine(@"wwwroot/imgCache/", t.fileName);
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error where delete " + t.fileName + " from disk. Exception:" + ex.Message);
                    }
                }

                Log.Information("Delete from db.");
                dbContext.screnshotCache.RemoveRange(
                            dbContext.screnshotCache.Where(x =>
                            x.timestamp.AddDays(clearCashInterval) > DateTime.Now)
                    );

                dbContext.SaveChanges();
                Log.Information("Clear " + clearTbCnt.ToString() + " in db cache tables.");

            }

            //Удаление из памяти.
            while(!_cancellationToken.IsCancellationRequested)
            {
                lock (lockCachePool)
                {
                   int cnt=poolCache.clearComplate();
                   Log.Information("Clear " + cnt.ToString() + " in memory cache tables.");

                    break;
                }

                Task.Delay(300);
            }                  

        }

        /// <summary>
        /// В таблицу browserError добавляю сообщение об ошибках браузера.
        /// level=1 -ошибки при попытке открыть url.
        /// level=2 ошибки похожие на краш браузера.
        /// level=3 ошибки при создании скрин шотта.
        /// 
        /// </summary>
        /// <param name="level"></param>
        /// <param name="messages"></param>
        private void saveBrowserError(int level,string messages, string url, string filename)
        {
            using (var scope=scopeFactory.CreateScope() )
            {
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                mBrowserErrors m = new mBrowserErrors();
                m.level = level;
                m.message = messages;
                m.url = url;
                m.filename = filename;
                m.created = DateTime.Now;

                db.browserErrors.Add(m);
                db.SaveChanges();
            }
        }

    }
}
