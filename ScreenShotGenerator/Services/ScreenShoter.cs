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
    ///Логика скриншоттера.
    /// </summary>
    public class ScreenShoter : IScreenShoter
    {
        private readonly IHttpContextAccessor _context;
        private readonly IServiceScopeFactory scopeFactory;

        //Директория для хранения временных файлов.
        const String tmpDir = "imgCache";

        //Полное имя хоста.
        private String hostName = null;

        /// <summary>
        /// Пул задач.
        /// </summary>
        private PoolTasks poolTask;

        /// <summary>
        /// Ид элементов в списке. Идентификатор элемента для возможности его сортировки по возрастанию
        /// </summary>
        int elementId = 0;


        /// <summary>
        ///Кэш уже созданых скриншотов. Хранит сведения о них. Заполняется в процессе работы сервиса.
        /// </summary>
        CacheRam Cache;

        /// <summary>
        /// Пул браузеров.
        /// </summary>
        BrowserPool browserPool;

        int poolBrowserSize = 1; //Количество запущенных браузеров.
        int browserTasksPerThread = 5; //Количество задач из пула которые браузер обрабатывает за раз.
        int clearCashInterval = 10; //Интервал очистки кеша, в часах.

        /// <summary>
        ///  Среднее время выполнения запроса.
        /// </summary>
        int averageTime = 2; 
        /// <summary>
        /// Максимальное кол-во запущенных браузеров
        /// </summary>
        int maxCountBrowser = 5;

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
        /// Токен завершения потока.
        /// </summary>
        CancellationToken _cancellationToken;

        /// <summary>
        /// Тайм аут загрузки страницы браузером.
        /// </summary>
        private int pageLoadTimeouts;
        /// <summary>
        /// Тайм аут загрузки скриптов браузером.
        /// </summary>
        private int javaScriptTimeouts;

        /// <summary>
        /// Счетчик размера выходных файлов, находящихся во временой папке.
        /// </summary>
        private UInt64 outFilesSize;

        /// <summary>
        /// Максимальный размер временной папки с файлами,в Кб.
        /// </summary>
        private UInt32 caсheSpaceLimit;

        /// <summary>
        /// Размер файлов которые должны остаться после достижения лимита и принудительной чистки кэша.
        /// </summary>
        private UInt32 cacheRemainingSize;

        /// <summary>
        /// Начат процесс принудительной чистки кеша.
        /// </summary>
        private bool beginForciblyCleanCaсhe;

        /// <summary>
        /// Список объектов для ожидания завершения браузером выполнения задачи.
        /// </summary>
        private List<waiterEvent> waiterEventList;


        public ScreenShoter(
            IHttpContextAccessor context,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _context = context;
            this.scopeFactory = scopeFactory;

            poolTask = new PoolTasks();
            waiterEventList = new List<waiterEvent>();

            Cache = new CacheRam();

            //Чтение настроек сервиса.
            readSettingsFromDb();

            //Выключает чтение кеши с базы данных. Использовать только для отладки.
            if (configuration["ScreenShoter:enableReadCacheFromDbInStart"] == "false")
            {
                enableReadCacheFromDbInStart = false;
            }

            createTimers(configuration); //Настраивает таймеры.
                   
            //Читаю таймауты из конфига.
            pageLoadTimeouts = parceIntCfgValue(configuration, "PageLoadTimeouts", 8);
            javaScriptTimeouts = parceIntCfgValue(configuration, "JavaScriptTimeouts", 8);

            //Из конфига считываю имя хоста.
            hostName = configuration["ScreenShoter:hostName"];

            //Максимальный размер папки кэша.
            int tmpDirLimit = parceIntCfgValue(configuration, "tmpDirLimit", 10240);
            caсheSpaceLimit = ((UInt32)tmpDirLimit) * 1024; //в Кб.

            //Общий размер оставшихся файлов после принудительной чистки кеша.
            int tmpDirRemainingSize = parceIntCfgValue(configuration, "tmpDirRemainingSize", 1024);
            cacheRemainingSize = ((UInt32)tmpDirRemainingSize) * 1024;

            //Перезагружать браузер после определенного количество скриншотов. 0-не перезагружать.
            int browserRestartAfterScreens = parceIntCfgValue(configuration, "browserRestartAfterScreens", 100);

            browserPool = new BrowserPool(tmpDir, ref poolTask,
                OnBrowserTaskCompleted);
            browserPool.saveBrowserErrorDg = saveBrowserError;
            browserPool.javaScriptTimeouts = javaScriptTimeouts;
            browserPool.pageLoadTimeouts = pageLoadTimeouts;
            browserPool.browserRestartAfterScreens = browserRestartAfterScreens;
        }


        /// <summary>
        /// Возвращает настройки сервиса для отображения на админ панеле.
        /// </summary>
        /// <returns></returns>
        public SystemSettingModel getSettings()
        {
            SystemSettingModel m = new SystemSettingModel();
            m.browserMin = poolBrowserSize;  //Количество запущенных браузеров.
            m.browserMax = maxCountBrowser;
            m.averageTimeRequest = averageTime;
            m.tasksAmount = browserTasksPerThread; //Количество задач из пула которые браузер обрабатывает за раз.
            m.clearCacheInterval = clearCashInterval; //Интервал очистки кеша, в часах.
            m.cacheElementsCnt = Cache.Count();

            //Количество элементов обрабатываемых на данный момент.
            m.curentElementsInProcessCnt = poolTask.curentElementsInProcessCnt();
            m.browserCount = browserPool.browserCount();

            return m;
        }

        /// <summary>
        /// Возвращает список задач у которых статус не новый.
        /// </summary>
        /// <param name="top"></param>
        /// <returns></returns>
        public List<mJobPool> getPoolTasksInfo(int top)
        {
            return poolTask.getItemInWork(top);
        }

        /// <summary>
        /// Возвращает lastCnt последних записей в кеши.
        /// </summary>
        /// <param name="lastCnt"></param>
        /// <returns></returns>
        public List<mCacheRam> getCacheItems(int lastCnt)
        {
            return Cache.getLastItems(lastCnt);
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
            browserPool.createPool(poolBrowserSize);//Создаем пул браузеров.

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
            browserPool.clearPool();
            Log.Information("StopService.");
        }

        public Task stopService(CancellationToken cancellationToken)
        {
            stopBrowserPool();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Возвращает размер файлов во временной папке в Мб.
        /// </summary>
        /// <returns></returns>
        public int getTmpDirSize()
        {
            return (int)outFilesSize / 1024;
        }


        /// <summary>
        /// Количество объектов в памяти.
        /// </summary>
        /// <returns></returns>
        public  int CacheItemsCount()
        {
            return Cache.Count();
        }

        /// <summary>
        /// Количество объектов на диске.
        /// </summary>
        /// <returns></returns>
        public List<mImageList> DiskItems()
        {
            //Получение списка имен файлов.
            string dirPath = @"wwwroot/imgCache";
            //var directory 
            IEnumerable<string> listNames = Directory
            .GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f));

            List<mImageList> fileNames = new List<mImageList>();
            foreach (string str in listNames)
            {
                //Пропускаю системные файлы.
                if (UrlErrorImg.IsSystemErrorPage(str)) continue;
                fileNames.Add(new mImageList() { name = "/imgCache/" + str }); ;
            }

            return fileNames;
        }

        /// <summary>
        /// Обработчик события по завершению каким либо браузером задачи(сделал скриншот).
        /// </summary>
        /// <param name="requestId"></param>
        private void OnBrowserTaskCompleted(string requestId)
        {

            //Поиск требуемого ид http запроса и сброс ожидания.
            foreach (var w in waiterEventList)
            {
                if (w.requestId == requestId)
                {
                    w.signalizator.Set();
                    break;
                }
            }
        }



        /// <summary>
        /// Увеличивает значение счетчика идентификатора задач.
        /// </summary>
        private void incElementId()
        {
            //Исключение ошибки переполнения, если сервис будет очень долго работать. -10 просто так.
            if (elementId == int.MaxValue - 10)
                elementId = 0;//Обнуляю.
            else
                elementId++;
        }



        /// <summary>
        /// Анализирует состояние пула, и возвращает модель ответа, если пул переполнен.
        /// Иначе =null
        /// </summary>
        /// <returns></returns>
        private bool allowAcceptNewTasks(string[] urls)
        {
            int countNewTask = urls.Length; //кол-во урлов в запросе (4)

            /* Простой алгоритм.*/
            //Количество задач ожидающих в пуле(статус 0);
            int waitTaskCount = poolTask.waitTasksCnt(); // Получаем Количество задач из списка со статусом WAIT (8)
                                                         //Количество задач который выполняют браузеры.
            // Получаем Количество задач из списка со статусом PROGRESS (4)
            int progressTaskCount = poolTask.curentElementsInProcessCnt();

            int maxCountTaskForBrowser = 60 / averageTime; // Макс. кол-во задач на браузер (2)
                                                           //countNewTask количество задач который мы хотим выполнять.
            int taskCount = countNewTask + waitTaskCount + progressTaskCount; // Кол-во задач для будущего выполнения (16)
            int needBrowserCount = taskCount / maxCountTaskForBrowser; // Кол-во браузеров для выполнения текущих задач (8)

            if (needBrowserCount > maxCountBrowser)
            { // Задачу добавить не можем, так как не справимся 
                return false;
            }

            int realWorkBrowser = browserPool.browserCount(); //Реальное кол-во запущенных браузеров
            // Проверяем необходимость запуска доп. браузеров
            if (needBrowserCount > realWorkBrowser)
            {
                //needBrowserCount-количество  браузеров которые должны работать в данный момент.
                browserPool.startNewBrowser(needBrowserCount); //Запускаю новые браузеры.
            }

            int minCountBrowser = poolBrowserSize; //Минимальное кол-во запущенных браузеров

            // Проверяем необходимость остановки доп. браузеров
            if ((realWorkBrowser - needBrowserCount) >= 2)
            {
                if (needBrowserCount < minCountBrowser)
                    needBrowserCount = minCountBrowser;
                else
                    needBrowserCount += 1;

                //needBrowserCount-количество  браузеров которые должны работать в данный момент.
                browserPool.leaveWorkBrowsers(needBrowserCount); //Оставляем работать требуемое количество.
            }

            return true;
        }

        /// <summary>
        /// Добавляет задачу в пул задач и ждет ее завершения.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public List<mUserJson> runJob(string[] urls, string userIP, string conUUID)
        {
            //Можем ли мы справиться с задачей?
             if(!allowAcceptNewTasks(urls))
            {
                //Не можем. Возвращаем json с сообщениями.
                return generateBusyAnswer(urls);
            }

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
                    t.imageSize = new ImageSize();
                    // t.imageSize.width = 600;
                    // t.imageSize.height = 400; для теста.
                    t.imageSize.width = 1280;
                    t.imageSize.height = 1060;

                    //Увеличивает значение счетчика идентификатора задач.
                    incElementId();

                    poolTask.add(t); //Добавляю задачу в пулл задач.
                   
                }
                else //Нашел выполненную.
                {
                    t = cashValue;
                }

                //Все задачи в пакете будут иметь одинаковый идентификатор запроса.
                t.requestId = conUUID;

                jb.Add(t);
            }


            int taskCnt = jb.Count; //Количество задач.
            var stopwatch = new Stopwatch();//Меряем сколько времени прошло.

            //Не находятся ли все задачи в кэш? и все уже выполнено..
            //Считаю количество выполненных задач или задач с ошибками.
            List<mUserJson> answer = checkComplatedJobs(jb, taskCnt, userIP);
            if (answer != null) return answer; //Все задачи выполнены, возвращаю результат.

            //Ожидатель завершения какой либо задачи для данного запроса.
            waiterEvent wE = new waiterEvent();
            wE.requestId = conUUID;
            waiterEventList.Add(wE); //Добавляю в общий "пул" ожидальщиков.

            //Генерируем событие-Информируем все браузеры что есть новая задача.
            browserPool.eventNewJobForBrowser();

            //Жду пока браузеры не обработают задачи. Или не остановят процесс.
            while (!_cancellationToken.IsCancellationRequested)
            {
                stopwatch.Start();

                wE.signalizator.WaitOne(); //Ждем пока браузер выполнит задачу из этого запроса.

                //Считаю количество выполненных задач или задач с ошибками.
                List<mUserJson> answ = checkComplatedJobs(jb, taskCnt, userIP);
                if (answ != null) return answ; //Все задачи выполнены, возвращаю результат.

                //Измеряем сколько обрабатываются задачи.
                stopwatch.Stop();
                double elipsed = stopwatch.Elapsed.TotalSeconds;
                if (elipsed > 50)
                {
                    //Устанавливает сообщение об ошибки по истечению тайм аута обработки.
                    setTimeOutError(ref jb);
                    return generateAnswer(ref jb, userIP);
                }

                //Все задачи не выполнены.  Если один браузер выполняет одну задачу-его нужно разбудить.
                browserPool.eventNewJobForBrowser();//Генерируем событие-Информируем все браузеры что есть новая задача.
            }

            //Для данного типа это обязательно согласно документации.
            wE.signalizator.Dispose();

            return null;

        }

        /// <summary>
        /// Проверяет выполнены ли все задачи. Если выполнены возвращает набор данных.
        /// </summary>
        /// <param name="jb"></param>
        /// <param name="taskCnt"></param>
        /// <param name="userIP"></param>
        /// <returns></returns>
        private List<mUserJson> checkComplatedJobs(List<mJobPool> jb, int taskCnt, string userIP)
        {

            //Считаю количество выполненных задач или задач с ошибками.
            int cntComplate = 0;
            foreach (mJobPool t in jb)
            {
                if ((t.status == (int)enumTaskStatus.End) || (t.status == (int)enumTaskStatus.Error))
                    cntComplate++;
            }


            //Все задачи выполнены.
            if (taskCnt == cntComplate)
            {
                return generateAnswer(ref jb, userIP);
            }

            return null;
        }





        /// <summary>
        /// Выполняет пост обработку выполненных зачач, и формирует ответ.
        /// </summary>
        /// <returns></returns>
        private List<mUserJson> generateAnswer(ref List<mJobPool> jb, string userIP)
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
                //Если новая задача.
                if (j.status == (int)enumTaskStatus.NewTask)
                {
                    j.fileName = "Tool long wait complete request from browser.";
                    j.status = 2;
                }
            }
        }

        /// <summary>
        /// Формирует сообщение о занятости сервиса.
        /// </summary>
        /// <param name="urls"></param>
        /// <returns></returns>
        private List<mUserJson> generateBusyAnswer(string[] urls)
        {
            List<mUserJson> userList = new List<mUserJson>();

            foreach (string url in urls)
            {
                mUserJson userLine = new mUserJson();
                userLine.url = url;
                userLine.status = 2;
                userLine.log = "Service overload";
                userList.Add(userLine);
            }

            return userList;
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
                    //Возвращается не стандартная ошибка. Файлы стандартных ошибок не добавляем в кэш.
                    if (UrlErrorImg.IsSystemErrorPage(j.fileName)) continue;

                    //Объект еще не находиться в кеши и обработан успешно.
                    if ((j.inCash == false) && (j.status == 3))
                    {
                        //Что бы не было явных пересечений при обработки одинаковых урл.
                        j.inCash = true; //Говорим что объект кеширован.

                        //Добавляю в кеш, если не заблокирован. Или ждем.
                        Cache.add(j);
                        
                        //Cохраняю в БД на случай перезагрузки сервера.
                        mCashTable line = new mCashTable();
                        line.url = j.url;
                        line.timestamp = j.timestamp;
                        line.fileName = j.fileName;
                        line.wastedTime = j.wastedTime;

                        db.screnshotCache.Add(line);
                        needSaveDb = true;
                        //Общий размер всех файлов.
                        outFilesSize += j.fileSize;
                    }
                }

                //Необходимо сохранить значения.
                if (needSaveDb)
                {
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error save Changes Db " + ex.Message);
                    }

                }

            }


            //Общий размер всех файлов превысил лимит,нужно чистить каталог.
            if (outFilesSize > caсheSpaceLimit)
            {
                Log.Information("Exceeded max dir limit. Begin clear dir.");
                Task t = new Task(() => clearCacheForcibly(cacheRemainingSize));
                t.Start();
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
                    l.fileSize = (uint)ct.size;
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
            settings.Add("averageTimeRequest", "");
            settings.Add("browserMax", "");
            
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

                maxCountBrowser= Convert.ToInt32(settings["browserMax"]);
                averageTime= Convert.ToInt32(settings["averageTimeRequest"]);
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
            settings["poolBrowserSize"] = m.browserMin.ToString();  //Количество запущенных браузеров.
            settings["browserTasksPerThread"] = m.tasksAmount.ToString(); //Количество задач из пула которые браузер обрабатывает за раз.
            settings["clearCashInterval"] = m.clearCacheInterval.ToString(); //Интервал очистки кеша, в часах.
            settings["browserMax"] = m.browserMax.ToString();
            settings["averageTimeRequest"] = m.averageTimeRequest.ToString();

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
            poolBrowserSize = m.browserMin; //Количество запущенных браузеров.
            browserTasksPerThread = m.tasksAmount; //Количество задач из пула которые браузер обрабатывает за раз.
            clearCashInterval = m.clearCacheInterval; //Интервал очистки кеша, в часах.
            maxCountBrowser=m.browserMax;
            averageTime=m.averageTimeRequest;
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
        /// Запуск процесса очистки при нажатии на кнопку.
        /// </summary>
        public void RunCleaning(List<mImageList> diskItems)
        {
            //Удаление файлов на диске.
            Log.Information("Run cleaning by user.");
            Log.Information("Delete from disk "+diskItems.Count()+" items.");

            string curentDirectory = Directory.GetCurrentDirectory();
            string rootDir=Path.Combine(curentDirectory,@"wwwroot");

            foreach (mImageList f in diskItems)
            {
                string path = rootDir+f.name;
                try
                {                  
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    Log.Error("Error where delete " + path + " from disk. Exception:" + ex.Message);
                    return;
                }
            }

            Log.Information("Delete from memory " + Cache.Count().ToString() + " items.");
            Cache.clearAll();
            Log.Information("End cleaning.");
        }

        /// <summary>
        /// Удаляет выполненные задачи из пула.
        /// </summary>
        private void ClearPoolTasks()
        {
            // Удалить завершенные задачи и задачи с ошибками.
            // Возвращает количество удаленных.
            int cnt = poolTask.clearComplate();
            Log.Information("Clear pool Task comlete. Clear=" + cnt.ToString());
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
                clearTbCnt = tb.Count();

                if (clearTbCnt == 0)
                {
                    Log.Information("Nothing clear.");
                    return;
                }


                //Удаление файлов на диске.
                Log.Information("Delete from disk." + clearTbCnt.ToString() + " items.");

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


                //Очистка ошибок браузера, что бы не переполнялось.
                Log.Information("Clear browser Error in Db.");
                dbContext.browserErrors.RemoveRange(
                        dbContext.browserErrors.Where(x =>
                        x.created.AddHours(clearCashInterval) < DateTime.Now)
                );



                try
                {
                    dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error("Error save Db." + ex.Message);
                }

                Log.Information("End clear " + clearTbCnt.ToString() + " in db cache tables.");

            }

                //Удаляет записи, которые хранились более  hour часов.
                int cnt = Cache.clearOld(clearCashInterval);
                Log.Information("Clear " + cnt.ToString() + " in memory cache tables.");
            
        }


        /// <summary>
        /// Принудительно очищаем кэш, оставляем только последние(самые новые) remainingSize записей. 
        /// </summary>
        private void clearCacheForcibly(UInt32 remainingSize)
        {
            if (beginForciblyCleanCaсhe) return; //Начата принудительная чистка кеша.
            beginForciblyCleanCaсhe = true;

            Log.Information("Forcibly clear cache after limit tmp dir.");

            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                //Вычисляем размер удаляемых файлов.
                UInt64 delSize = outFilesSize - ((UInt64)remainingSize);

                //Удаление файлов на диске.
                Log.Information("Forcibly delete from disk and Db after limit. Will be clean " + delSize.ToString() +
                    "Kb.");

                //Выбираю элементы которые нужно удалить.
                List<mCacheRam> delRam = Cache.getFirstElementsSomeSize(delSize);
                int itemCount = delRam.Count; //Количество удаляемых записей.

                //Удаление из памяти.
                 Cache.clearInterval(delRam);
              

                foreach (mCacheRam t in delRam)
                {
                    try
                    {
                        var path = Path.Combine(@"wwwroot/imgCache/", t.fileName);
                        File.Delete(path);
                        //Удаляем из БД.
                        dbContext.screnshotCache.Remove(dbContext.screnshotCache.
                            Where(x => x.fileName == t.fileName).First());
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error where delete " + t.fileName + " from disk. Exception:" + ex.Message);
                    }


                }



                Log.Information("End Forcibly delete. Remove " + itemCount.ToString() + " items.");
                //Ставим истинный размер текущего кэша.
                outFilesSize -= delSize;

                try
                {
                    dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error("Error save Db." + ex.Message);
                }
            }

            beginForciblyCleanCaсhe = false;

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
        private void saveBrowserError(int level, string messages, string url, string filename)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                mBrowserErrors m = new mBrowserErrors();
                m.level = level;
                m.message = messages;
                m.url = url;
                m.filename = filename;
                m.created = DateTime.Now;

                db.browserErrors.Add(m);

                try
                {
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error("Error save Changes Db " + ex.Message);
                }
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
            int interval2 = parceIntCfgValue(configuration, "intervalCheckNeedClearCash", 90);
            interval2 *= 60000;

            //Интревал запуска таймера после старта приложения.
            int checkNeedClearCashAfterStartup = parceIntCfgValue(configuration, "CheckNeedClearCashAfterStartup", 90);


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
        private static int parceIntCfgValue(IConfiguration configuration, string parametrName, int defaultValue)
        {
            int parce;

            if (!Int32.TryParse(configuration["ScreenShoter:" + parametrName], out parce))
            {
                Log.Error("Can't convert ScreenShoter:" + parametrName + " to Int32. Bad value:" +
                   configuration["ScreenShoter:" + parametrName] + ". Set deafault value " +
                   defaultValue.ToString() + ".");
                parce = defaultValue;
            }

            return parce;
        }


    }
}
