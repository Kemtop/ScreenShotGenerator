using Microsoft.AspNetCore.Http;
using ScreenShotGenerator.Services.BrowserControl;
using ScreenShotGenerator.Services.ScreenShoterPools;
using System;
using System.Collections.Generic;
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
using ScreenShotGenerator.Services.Models;
using System.Diagnostics;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Делегат для сохранения ошибок браузера.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="message"></param>
     public delegate void saveBrowserError(int level,string message,string url,string filename);

    /// <summary>
    ///Логика скриншоттера.
    /// </summary>
    public class ScreenShoter : IScreenShoter
    {
        private readonly IHttpContextAccessor _context;
        private readonly IServiceScopeFactory scopeFactory;

        //Синхронизация потоков, для работы с общим пулом.
        static object lockPoolTask = new object();

        //Блокировка кеши poolCache.
        static object lockCachePool = new object();

        //Директория для хранения временных файлов.
        const String tmpDir = "imgCache";

        //Полное имя хоста.
        private String hostName = null;

        /// <summary>
        /// Пул задач.
        /// </summary>
        poolTasks poolTask;

        /// <summary>
        /// Ид элементов в списке. Идентификатор элемента для возможности его сортировки по возрастанию
        /// </summary>
        int elementId = 0;


        /// <summary>
        ///Кэш уже созданых скриншотов. Хранит сведения о них. Заполняется в процессе работы сервиса.
        /// </summary>
        cacheRam Cache;

        //Пул объектов для управления браузерами.
        List<BrowserControlLogic> poolBrowserControls;

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

        /// <summary>
        /// Запущен процесс очистки кеши сервиса.
        /// </summary>
        private bool runClearCache;

        /// <summary>
        /// Токен завершения потока.
        /// </summary>
        CancellationToken _cancellationToken;

        /// <summary>
        /// Делегат для записи ошибок браузера в БД.
        /// </summary>
        saveBrowserError saveBrowserErrorDg;


        /// <summary>
        /// Тайм аут загрузки страницы браузером.
        /// </summary>
        int pageLoadTimeouts;
        /// <summary>
        /// Тайм аут загрузки скриптов браузером.
        /// </summary>
        int javaScriptTimeouts;


        public ScreenShoter(
            IHttpContextAccessor context,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _context = context;
            this.scopeFactory = scopeFactory;

            poolTask = new poolTasks();
           
            Cache = new cacheRam();
            poolBrowserControls = new List<BrowserControlLogic>();

            //Чтение настроек сервиса.
            readSettingsFromDb();

            //Выключает чтение кеши с базы данных. Использовать только для отладки.
            if (configuration["ScreenShoter:enableReadCacheFromDbInStart"] == "false")
            {
                enableReadCacheFromDbInStart = false;
            }

            createTimers(configuration); //Настраивает таймеры.

            //Делегат для записи ошибок.
            saveBrowserErrorDg = saveBrowserError;

            //Читаю таймауты из конфига.
            pageLoadTimeouts = parceIntCfgValue(configuration, "PageLoadTimeouts", 8);
            javaScriptTimeouts = parceIntCfgValue(configuration, "JavaScriptTimeouts", 8);

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
            m.cacheElementsCnt = Cache.Count();

            //Количество элементов обрабатываемых на данный момент.
            m.curentElementsInProcessCnt = poolTask.curentElementsInProcessCnt();

            return m;
        }

        /// <summary>
        /// Возвращает список задач у которых статус не новый.
        /// </summary>
        /// <param name="top"></param>
        /// <returns></returns>
        public List<mJobPool> getPoolTasksInfo(int top)
        {
            // Ожидает пока пул задач будет очищен, или прийдет сигнал остановки потока.
            waitUnlockClearManPoolTask();
            lock(lockPoolTask)
            {
                return poolTask.getItemInWork(top);
            }
            
        }

        /// <summary>
        /// Возвращает lastCnt последних записей в кеши.
        /// </summary>
        /// <param name="lastCnt"></param>
        /// <returns></returns>
        public List<mCacheRam> getCacheItems(int lastCnt)
        {
            //Нужно гарантированно вернуть результат.
            //Жду пока разблокируеться объект или не прийдет сигнал остановки.
            while (!_cancellationToken.IsCancellationRequested)
            {
                //Если запущен процесс чистки кеша. Жду окончания.
                if(runClearCache)
                {
                    Task.Delay(300);
                    continue;
                }

             
                lock (lockCachePool)
                {
                    return Cache.getLastItems(lastCnt);
                }

                //Жду пока ресурс разблокируеться.
                Task.Delay(300);
            }

            return null;
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
            foreach (BrowserControlLogic bl in poolBrowserControls)
            {
                Log.Information("Close browser..." + i.ToString());
                bl.stopProcess();
                i++;
            }

            //Очищаю пулл.
            poolBrowserControls.Clear();

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
                    //Создаем экземпляр обьекта для управления браузером.
                    BrowserControlLogic Bl = new BrowserControlLogic(
                        new ImpBrowserControlFireFox(pageLoadTimeouts, javaScriptTimeouts),//Задаю таймауты загрузки.
                        saveBrowserErrorDg, tmpDir) ;//new ImpBrowserControlChrome();
                    Bl.tasksPerThread = browserTasksPerThread; //Количество задач из пула которые браузер обрабатывает за раз.
                    Bl.browserId=i + 1; //Ид браузера, что бы потоки как то можно отличать.
               
                    if (!Bl.startBrowser())//Запустить браузер. Выходим если не смог.
                        break;
                                        
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
        /// Ожидает пока пул задач будет очищен, или прийдет сигнал остановки потока.
        /// </summary>
        private void waitUnlockClearManPoolTask()
        {
            //Если начат процесс очистки пула, ждем завершения.
            while (runClearPoolTasks)
            {
                Task.Delay(1000);
                if (_cancellationToken.IsCancellationRequested) return;
            }
        }


        /// <summary>
        /// Анализирует состояние пула, и возвращает модель ответа, если пул переполнен.
        /// Иначе =null
        /// </summary>
        /// <returns></returns>
        private mJobPool allowAcceptNewTasks(string url)
        {
            mJobPool task = null;
            int cnt = poolTask.curentWaitElements(); //Количество ожидающих задач.
            //Больше чем браузер обрабатывает за раз.
            if (cnt > browserTasksPerThread+25)
            {
                task = new mJobPool();
                task.id = -1;
                task.url = url;
                task.status =(int)enumTaskStatus.Error;
                task.fileName = "Too many waiting tasks in pool. Now "+cnt.ToString();
            }

            return task;
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
                mJobPool cashValue = Cache.findUrl(url);

                if (cashValue == null) //Задача не была выполнена,добавляю в пул.
                {
                    t = new mJobPool(); //Новая задача.
                    t.url = url;
                    t.id = elementId; //Идентификатор элемента для возможности его сортировки.

                    //Исключение ошибки переполнения, если сервис будет очень долго работать. -10 просто так.
                    if (elementId == int.MaxValue - 10)
                        elementId = 0;//Обнуляю.
                    else
                        elementId++;

                    waitUnlockClearManPoolTask(); //Если осуществляется очистка пула-ждем.
                                                  //Не переполнен ли пул задач?
                    mJobPool deny = allowAcceptNewTasks(url);
                    if (deny == null)
                        poolTask.add(t); //Добавляю задачу в пулл задач.
                    else
                        t = deny; //Сообщение об ошибке переполения пула.

                }
                else //Нашел выполненную.
                {
                    t = cashValue;
                }

                jb.Add(t);
            }


            int taskCnt = jb.Count; //Количество задач.

            int cntComplate = 0; //Количество выполненных задач.
            var stopwatch = new Stopwatch();//Меряем сколько времени прошло.
          
            //Жду пока браузеры не обработают задачи. Или не остановят процесс.
            while (!_cancellationToken.IsCancellationRequested)
            {
                stopwatch.Start();

                //Считаю количество выполненных задач или задач с ошибками.
                cntComplate = 0;
                foreach (mJobPool t in jb)
                {
                    if ((t.status == (int)enumTaskStatus.End) || (t.status == (int)enumTaskStatus.Error))
                        cntComplate++;
                }


                //Все задачи выполнены.
                if (taskCnt == cntComplate)
                {
                    return generateAnswer(ref jb,userIP);
                }

                //Измеряем сколько обрабатываются задачи.
                stopwatch.Stop();
                double elipsed = stopwatch.Elapsed.TotalSeconds;
                if(elipsed>50)
                {
                    //Устанавливает сообщение об ошибки по истечению тайм аута обработки.
                    setTimeOutError(ref jb);
                    return generateAnswer(ref jb, userIP);
                }

               
            }

            return null;

        }



        /// <summary>
        /// Выполняет пост обработку выполненных зачач, и формирует ответ.
        /// </summary>
        /// <returns></returns>
        private List<mUserJson> generateAnswer(ref List<mJobPool> jb,string userIP)
        {
            //Добавляет сведения о выполенных задачах в кеш.
            addToCash(ref jb);
            logErrors(ref jb, userIP);//Сохраняю сведени об ошибках.

            //Преобразовываю в пользовательский json.
            return createUserJson(ref jb);

        }

        /// <summary>
        /// Устанавливает сообщение об ошибки по истечению тайм аута обработки.
        /// </summary>
        /// <param name="jb"></param>
        private void setTimeOutError(ref List<mJobPool> jb)
        {
            foreach (mJobPool j in jb)
            {
                //Возникла ошибка.
                if (j.status == (int)enumTaskStatus.NewTask)
                {
                    j.fileName = "Tool long wait complete request from browser.";
                    j.status = 2;
                }
            }
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
                if (j.status == (int)enumTaskStatus.End)
                {
                    String fullname = hostName + "/" + tmpDir + "/" + j.fileName;
                    userLine.path = fullname;
                    userLine.status = 1;
                }

                //Возникла ошибка.
                if (j.status == (int)enumTaskStatus.Error)
                {
                    userLine.log = j.fileName;
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
                    Cache.add(j);
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
                        line.wastedTime = j.wastedTime;

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
                    mCacheRam l = new mCacheRam();
                    l.id = ct.Id;
                    l.fileName = ct.fileName;
                    l.timestamp = ct.timestamp;
                    l.url = ct.url;
                    l.wastedTime = ct.wastedTime;
                    Cache.add(l);
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
                    string str = userIP + ";" + j.url + ";" + j.fileName;
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

            //Передаю настройки.
            poolBrowserSize = m.browserAmount; //Количество запущенных браузеров.
            browserTasksPerThread = m.tasksAmount; //Количество задач из пула которые браузер обрабатывает за раз.
            clearCashInterval = m.clearCashInterval; //Интервал очистки кеша, в часах.
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
                lock (lockPoolTask)
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

                //Текущая дата больше чем дата создания записи+интервал чистки.
                List<mCashTable> tb = dbContext.screnshotCache.Where(x =>
                 x.timestamp.AddHours(clearCashInterval) < DateTime.Now).ToList();
                clearTbCnt=tb.Count();

                if(clearTbCnt==0)
                {
                    Log.Information("Nothing clear.");
                    return;
                }


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
                            x.timestamp.AddHours(clearCashInterval) < DateTime.Now)
                    );

                dbContext.SaveChanges();
                Log.Information("End clear " + clearTbCnt.ToString() + " in db cache tables.");

            }

            //Удаление из памяти.
            while(!_cancellationToken.IsCancellationRequested)
            {
                lock (lockCachePool)
                {
                    //Удаляет записи, которые хранились более  hour часов.
                    runClearCache = true;
                    int cnt =Cache.clearOld(clearCashInterval);
                    runClearCache = false;
                    Log.Information("Clear " + cnt.ToString() + " in memory cache tables.");

                    break;
                }

                //Жду пока ресурс разблокируеться.
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


        /// <summary>
        /// Настраивает таймеры.
        /// </summary>
        private void createTimers(IConfiguration configuration)
        {
            //Очистка завершенных задач.В минутах.
            int interval1 = parceIntCfgValue(configuration, "ClearComplatePoolTasks", 60);
            
            interval1 *= 60000; //Переводим в минуты.
            timerClearComplatePoolTasks = new Timer((Object stateInfo) =>
            {
                ClearPoolTasks();
            }, null, interval1, interval1);


            //Таймер запускающий задачу проверки необходимости очистки кеша.
            //Проверки корректности данных. Если пользователь введет ерунду.
            int interval2 = parceIntCfgValue(configuration, "intervalCheckNeedClearCash",90);           
            interval2 *= 60000;

            //Интревал запуска таймера после старта приложения.
            int checkNeedClearCashAfterStartup= parceIntCfgValue(configuration, "CheckNeedClearCashAfterStartup",90);
          

            timerClearCache = new Timer((Object stateInfo) =>
            {
                clearCache();
            }, null, checkNeedClearCashAfterStartup, interval2);
        }


        /// <summary>
        /// Получает из конфига приложения(appsetting.json) значение для указанного параметра(parametrName)
        /// пытаеться преобразовать его в Int32, в случае ошибки выводит сообщение в лог и возвращает defaultValue;
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="parametrName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private int parceIntCfgValue(IConfiguration configuration,string parametrName,int defaultValue)
        {
            int parce;

            if (!Int32.TryParse(configuration["ScreenShoter:"+ parametrName], out parce))
            {
                Log.Error("Can't convert ScreenShoter:"+ parametrName + " to Int32. Bad value:" +
                   configuration["ScreenShoter:" + parametrName] + ". Set deafault value "+
                   defaultValue.ToString()+".");
                parce = defaultValue;
            }

            return parce;
        }


    }
}
