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

namespace ScreenShotGenerator.Services
{
    public class ScreenShoter : IScreenShoter
    {
        private readonly IHttpContextAccessor _context; 
        private readonly ILogger<Worker> logger;
        private readonly DatabaseContext _databaseContext = new DatabaseContext();

        private readonly IServiceScopeFactory scopeFactory;

        //Синхронизация потоков.
        static object locker = new object();

        private int number = 0;

        //Директория для хранения временных файлов.
        const String tmpDir = "imgCache";

        //Полное имя хоста.
        private String hostName=null;
                

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
        int browserTasksPerThread=5; //Количество задач из пула которые браузер обрабатывает за раз.
        int clearCashInterval = 10; //Интервал очистки кеша, в часах.


        public ScreenShoter(ILogger<Worker> logger,
            IHttpContextAccessor context,
            IServiceScopeFactory scopeFactory)
        {
            this.logger = logger;
            _context = context;
            this.scopeFactory = scopeFactory;

            poolTask = new poolTasks();
            poolTask.tmpDir = tmpDir; //Директория для хранения скриншотов.

            poolCache = new poolTasks();
            poolBrowserControls = new List<IBrowserControl>();

            //Ведение логов.
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .WriteTo.File(@"./Logs/log.txt", rollingInterval: RollingInterval.Day, 
               outputTemplate:"{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
               .CreateLogger();


            //Чтение настроек сервиса.
            readSettingsFromDb();
        }

        /// <summary>
        /// Возвращает настройки сервиса для отображения на админ панеле.
        /// </summary>
        /// <returns></returns>
        public SystemSettingModel getSettings()
        {
            SystemSettingModel m = new SystemSettingModel();
            m.browserAmount = poolBrowserSize;  //Количество запущенных браузеров.
            m.tasksAmount= browserTasksPerThread; //Количество задач из пула которые браузер обрабатывает за раз.
            m.clearCashInterval=clearCashInterval; //Интервал очистки кеша, в часах.
            m.cacheElementsCnt = poolCache.cacheCnt();

            //Количество элементов обрабатываемых на данный момент.
            m.curentElementsInProcessCnt=poolTask.curentElementsInProcessCnt();

            return m;
        }
              
        /// <summary>
        /// Запускает сервис.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task runService(CancellationToken cancellationToken)
        {
            await Task.Delay(2000);
            Log.Information("Running service...");
            //createBrowserPool();//Создаем пул браузеров.
            Log.Information("It running.");


            //Считывает данные кеш из базы данных в память(объект poolCash).
            //readFromDbToCash();


            Thread thread3 = new Thread(() =>
            {

                while (true)
                {
                    //logger.LogInformation("Hello_3_");
                    Task.Delay(5000);
                }

            });
            //thread3.Start();


            while (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref number);
                //logger.LogInformation($"Worker printing number {number}");
                await Task.Delay(1000 * 5);
            }

        }


        public Task stopService(CancellationToken cancellationToken)
        {
            Log.Information("Stoping services...");
            //if (Browser != null) Browser.Quit();
            int i = 1;
            foreach (IBrowserControl bc in poolBrowserControls)
            {
                Log.Information("Close browser..."+i.ToString());
                bc.stopBrowser();
                i++;
            }

            logger.LogInformation("StopService");
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
                    Bc.setTaskId(i+1); //Ид браузера, что бы потоки как то можно отличать.
                    Bc.startBrowser();//Запустить браузер.
                                      //Пока не понятно нужна ли тут задержка.
                    Bc.processPool(ref poolTask, ref locker); //Запустить обработку пула задач.
                    poolBrowserControls.Add(Bc);
                }
                catch(Exception ex)
                {
                    Log.Error("Exception in [createBrowserPool]:" + ex.Message);
                }
            }

        }




        /// <summary>
        /// Добавляет задачу в пул задач и ждет ее завершения.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public List<mUserJson> runJob(string[] urls, string userIP)
        {
            //Получение корневого url, если уже его не получили.
            getHostName();


            //Список для быстрого мониторинга отправленных задач.
            List<mJobPool> jb = new List<mJobPool>();

             //Прохожу по списку урл.          
            foreach (string url in urls)
            {
                mJobPool t; //Задача.

               //Поиск выполненной задачи в кеш сервиса.
               mJobPool cashValue = poolCache.findUrl(url);

                if(cashValue==null) //Задача не была выполнена,добавляю в пул.
                {
                    t = new mJobPool(); //Новая задача.
                    t.url = url;
                    t.id = elementId; //Идентификатор элемента для возможности его сортировки.

                    //Исключение ошибки переполнения. -10 просто так.
                    if (elementId == int.MaxValue - 10)
                        elementId = 0;//Обнуляю.
                    else
                        elementId++;

                    poolTask.add(t); //Добавляю задачу в пулл задач.
                }
                else //Нашел выполненную.
                {
                    t = cashValue;                    
                }

                jb.Add(t);                
            }

      
            //Жду пока браузеры не обработают задачи.
            bool runing = true;

            int taskCnt = jb.Count; //Количество задач.

            int cntComplate = 0; //Количество выполненных задач.
            while(runing)
            {
                //Считаю количество выполненных задач или задач с ошибками.
                cntComplate = 0;
                foreach(mJobPool t in jb)
                {
                    if ((t.status == 3)|| (t.status == 2)) cntComplate++;
                }


                //Все задачи выполнены.
                if(taskCnt==cntComplate)
                {                   
                    //Добавляет сведения о выполенных задачах в кеш.
                    addToCash(ref jb);
                    logErrors(ref jb, userIP);//Сохраняю сведени об ошибках.

                    //Преобразовываю в пользовательский json.
                    return createUserJson(ref jb);
                   
                }

                Thread.Sleep(500);
            }

            return null;

        }


        /// <summary>
        /// Получение корневого url, если уже его не получили.
        /// </summary>
        private void getHostName()
        {
            if(hostName==null)
            {
                var request = _context.HttpContext.Request;
                hostName = request.Scheme + "://" + request.Host.Value;
                poolTask.hostName = hostName; //Передаю значение другим потокам.
                poolTask.tmpDir = tmpDir;//Директория в которой храняться картинки.
                logger.LogInformation("get hostname");
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
                if(j.status == 2)
                {
                    userLine.log = j.path;
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

            foreach (mJobPool j in jb)
            {
                //Объект еще не находиться в кеши и обработан успешно.
                if((j.inCash==false)&&(j.status==3))
                {
                    j.inCash = true; //Говорим что объект кеширован.
                    poolCache.add(j);

                    //Cохраняю в БД на случай перезагрузки сервера.
                    mCashTable line = new mCashTable();
                    line.url = j.url;
                    line.timestamp = j.timestamp;
                    line.fileName = j.fileName;

                    // var cashTable = _databaseContext.cashTable.Count();
                    _databaseContext.cashTable.Add(line);
                    needSaveDb = true;                   
                }           
            }

            //Необходимо сохранить значения.
            if(needSaveDb)
            _databaseContext.SaveChanges();

        }
               

        /// <summary>
        /// Считывает данные кеш из базы данных в память(объект poolCash).
        /// </summary>
        private void readFromDbToCash()
        {
            //Пулучаю данные из таблицы в виде списка.
            List<mCashTable> table = _databaseContext.cashTable.ToList();

            foreach(mCashTable ct in table)
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
                    string str = userIP+";"+j.url+";"+j.path;
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
                
                foreach(KeyValuePair<string,string> item in settings)
                {
                   settings[item.Key]= dbContext.serviceSettings.Where(x=>x.Name==item.Key).
                        FirstOrDefault().Value;
                }

               poolBrowserSize=Convert.ToInt32(settings["poolBrowserSize"]);  //Количество запущенных браузеров.
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
            settings["poolBrowserSize"] =m.browserAmount.ToString();  //Количество запущенных браузеров.
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


    }
}
