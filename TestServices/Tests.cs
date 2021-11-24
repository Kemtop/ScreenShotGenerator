using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Web;

namespace TestServices
{
    class Tests
    {
        //Список url.
        List<mTableWebicons> urls = new List<mTableWebicons>();
        object lockUrlsList = new object();//Объект для блокировки общиго ресурса.
        object lockDb = new object();//Объект для блокировки доступа к БД.

        //Количество скриншотов выполняемые одним потоком за раз.
        private int screeShotPerThread = 5;

        dbContext db = new dbContext();

        public Tests()
        {
            db.Database.Migrate();

            //Подключаю serilog.  
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Information()
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) //Выводить только варнинги Microsoft.
           .WriteTo.Console()
           .WriteTo.File(
                @"./Logs/log.txt",
                shared: true, //Доступен всем процессам.
                rollingInterval: RollingInterval.Day,
                flushToDiskInterval: TimeSpan.FromSeconds(20),
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
           .CreateLogger();
        }


        /// <summary>
        /// Запускает однопоточный тест.
        /// </summary>
        public void runTest1(string[] args)
        {
            //Данные хостов.
            Dictionary<int, string> hosts = new Dictionary<int, string>();
            hosts.Add(1, "https://localhost:44350");
            hosts.Add(2, "http://192.168.195.130:5000");
            hosts.Add(3, "http://localhost:5000");

            int tasks = 10;
            int hostKey = 3;
            //Нет параметров.
            if (args.Length==0)
            {
                //Спрашиваю пользователя.
                tasks = getFromUserTaskPerRequest(10);
                //Спрашиваю у пользователя.
                hostKey = getFromUserНost(hosts);
            }


            int beginLine=beginFromLine();//Строка с которой нужно начинать тест.
            Log.Information("Run Test1.");
            Console.WriteLine("Read file.");
            // prepareRundomFile(); //Ручная генерация.

            //Читаю файл в список.
            readFileFormatLinks(@"links.txt", beginLine);

            if (beginLine>0)
            {
                string info = "Test begin from " + beginLine + " lines. url=" + urls[0].url;
                Log.Information(info);
                Console.WriteLine(info);
            }

            taskTest1(1,tasks,hosts[hostKey],beginLine);
        }

        /// <summary>
        /// Читает файл beginFrom.txt, и запускает тест с указанной в нем строки.
        /// Если файла нет, возвращает 0.
        /// </summary>
        /// <returns></returns>
        private int beginFromLine()
        {
           string curentDirectory = Directory.GetCurrentDirectory();
           string filePath=curentDirectory + @"/beginFrom.txt";

            bool exists = System.IO.File.Exists(filePath);
            if (!exists) return 0;

            List<string> lines = System.IO.File.ReadLines(filePath).ToList();
            int value = 0;
            if(!Int32.TryParse(lines[0],out value))
            {
                string info = "Bad value in first line " + filePath + ". Test run from 0 line";
                Log.Error(info);
                Console.WriteLine(info);
            }

            return value;
        }

        /// <summary>
        /// Спрашиваю у пользователя сколько задач отправлять за один запрос.
        /// </summary>
        /// <returns></returns>
        private int getFromUserTaskPerRequest(int defaultValue)
        {
                Console.WriteLine("Enter amount of tasks per request, or press Enter to set defaul value "
                    +defaultValue.ToString());

            int res = 0;
            //Ждем пока пользователь введет число.
            while (true)
            {
                string key = Console.ReadLine();
                //Пользователь нажал Enter.
                if (String.IsNullOrEmpty(key)) return defaultValue;

                if (Int32.TryParse(key, out res))
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Error value!");
                }

            }

            return res;
        }

               
        /// <summary>
        /// Спрашиваю у пользователя хост.
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns></returns>
        private int getFromUserНost(Dictionary<int, string> hosts)
        {
            
            Console.WriteLine("Select host.");
            foreach(KeyValuePair<int,string> p in hosts)
            {
                Console.WriteLine(p.Key.ToString()+" "+p.Value);
            }

            int res = 0;
            //Ждем пока пользователь введет число.
            while (true)
            {
                string key = Console.ReadLine();
                if (Int32.TryParse(key, out res))
                {
                    //Проверяю что ввел пользователь.
                    foreach (KeyValuePair<int, string> p in hosts)
                    {
                        if(p.Key==res) return res; 
                    }

                    //Ввел ерунду.
                    Console.WriteLine("Bad value!");

                }
                else
                {
                    Console.WriteLine("Error value!");
                }

            }            
        }



        /// <summary>
        /// Читает файл в котором на каждой строке находится только url сайта.
        /// </summary>
        /// <param name="path"></param>
        public void readFileFormatLinks(string path,int beginFromLine)
        {
            List<string> lines = System.IO.File.ReadLines(path).Skip(beginFromLine).ToList();
            int id = 0;            
            foreach (string line in lines)
            {
                mTableWebicons m = new mTableWebicons();
                m.url = line.Trim();
                m.id = id;
                id++;

                urls.Add(m);
            }
        }


        /// <summary>
        /// Метод преобразования структуированных по алфавиту строк в файле, 
        /// в файл с перемешанным списком. Вызывать только из студии, есть проблеммы 
        /// с большим количеством записей.
        /// </summary>
        void prepareRundomFile()
        {
            List<mTableWebicons> tmp = new List<mTableWebicons>();
            readFilePhpMyAdmin(ref tmp, @"websites_online.sql", "INSERT INTO `websites_online` (`element_url`) VALUES",0);

            //Так как записи отсортированы, рандомизируем их.
            Random rnd = new Random();
            int len = tmp.Count;

            for (int i = 0; i < 50000; i++)
            {
                while (true)
                {
                    int pos = rnd.Next(0, len);

                    //Есть ли такой элемент в списке?
                    if (urls.Where(x => x.id == pos).Count() == 0)
                    {
                        string url = tmp[pos].url;

                        //Убираю личные документы.
                        if (url.Contains("docs")) continue;
                        urls.Add(tmp[pos]);

                        break;
                    }
                }

            }

            Console.WriteLine("Write to file");

            using (StreamWriter w = File.AppendText("links.txt"))
            {
                foreach(mTableWebicons line  in urls)
                {
                    //Проверка уникальности.
                    if(urls.Where(x => x.id==line.id).Count()>1 )
                    {
                        Console.WriteLine("Find povtor");
                        continue;
                    }

                    w.WriteLine(line.url);
                }

                
            }
                                    
        }


        /// <summary>
        /// Преобразовывает файл из вида вышрузки phpMyAdmin в текстовый файл.
        /// </summary>
        public void ConvertFile()
        {
            Console.WriteLine("You most remove all rubbish from file.");
            string fileName = @"websites_online.sql";
            string beginblock = "INSERT INTO `websites_online` (`id`, `element_url`) VALUES";
            Console.WriteLine("Read file " + fileName);
            List<mTableWebicons> lines = new List<mTableWebicons>();
            readFilePhpMyAdmin(ref lines,fileName,beginblock,1);
            Console.WriteLine("Read "+lines.Count().ToString()+".");

            string outFileName = "links.txt";
            Console.WriteLine("Write to file "+outFileName);

            using (StreamWriter w = File.AppendText("links.txt"))
            {
                foreach (mTableWebicons line in lines)
                {
                    w.WriteLine(line.url);
                }
            }

            Console.WriteLine("End");
        }



        /// <summary>
        /// Читает файл с http запросами. Вида
        /// INSERT INTO `websites_online` (`element_url`) VALUES указанными в beginblock.
        // (' https://yandex.ru/?clid=2299450'),
        ///('http:// Angryfox.info'),
        /// </summary>
        /// <param name="urls"></param>
        private void readFilePhpMyAdmin(ref List<mTableWebicons> urls,string fileName,string beginblock,int urlPos)
        {
            int id = 0;
            List<string> lines=System.IO.File.ReadLines(fileName).ToList();

            // Read the file and display it line by line.  
            bool enableBlock = false;
            string line = "";

            foreach (string line_ in lines)
            {
                line = line_;
                if (String.IsNullOrEmpty(line)) continue;
                if (line.Length<3) continue;

                //Начало вставки в таблицу.
                if (line.Contains(beginblock))
                {
                    enableBlock = true;
                    continue;
                }

                //Конец блока данных
                if (enableBlock)
                {
                    string str = line.Substring(line.Length - 3);
                    if(str.Contains("');"))
                    {
                        line = line.Substring(0,line.Length-1);
                    }
                    //enableBlock = false;
                }

                //Разрешено чтение блока.
                if (enableBlock)
                {
                    String[] arr = line.Split(',');
                    if (arr.Length < urlPos+1) continue; //Обход мусора в файле.
                    mTableWebicons m = new mTableWebicons();                   
                    m.url = arr[urlPos].Replace("'", "");
                    m.url = m.url.Replace("(", "");
                    m.url = m.url.Replace(")", "");

                    m.url = m.url.Trim();
                    m.id = id;
                    id++;

                    urls.Add(m);

                }

            }

        }


        /// <summary>
        /// Читаю файл первоначального формата. Будет ли он еще не понятно.
        /// </summary>
        /// <param name="urls"></param>
        public void readFile(ref List<mTableWebicons> urls)
        {

            // Read the file and display it line by line.  
            bool enableBlock = false;
            foreach (string line in System.IO.File.ReadLines(@"webicons_list.sql"))
            {

                //Начало вставки в таблицу.
                if (line == "INSERT INTO `webicons_list` (`id`, `host`, `last_up`, `is_create`) VALUES")
                {
                    enableBlock = true;
                    continue;
                }


                if (enableBlock && (line.Contains(';')))
                {
                    enableBlock = false;
                }

                //Разрешено чтение блока.
                if (enableBlock)
                {
                    String[] arr = line.Split(',');
                    mTableWebicons m = new mTableWebicons();
                    m.id = Convert.ToInt32(arr[0].Replace('(', ' '));
                    m.url = arr[1].Replace("'", "");
                    m.url = m.url.Trim();

                    urls.Add(m);

                }

            }

        }

        //Старый тест, переделать и использовать таски!
        public void run()
        {
            //Читаю данные.
            readFile(ref urls);
            //Начинаю тест с позиции 381.
            int i = 0;
            foreach (mTableWebicons m in urls)
            {
                if (i > 380) break;
                m.status = 1;
                i++;
            }

            Thread thread1 = new Thread(() => taskTest(1));
            Thread thread2 = new Thread(() => taskTest(2));
            Thread thread3 = new Thread(() => taskTest(3));

            thread1.Start();
            thread2.Start();
            thread3.Start();

            Console.WriteLine("End");
            int y = 0;

        }


        /// <summary>
        /// Поток выполняющий тестирование сервиса.
        /// </summary>
        private void taskTest(int threadNum)
        {
            List<mTableWebicons> data;
            bool work = true;
            Stopwatch stopWatch = new Stopwatch(); //Измеряем время выполнения сервером запроса.

            while (work)
            {

                lock (lockUrlsList)
                {
                    //Получаю первые screeShotPerThread не обработанных url.
                    IEnumerable<mTableWebicons> dataLinq = urls.Where(x => x.status == 0).OrderBy(x => x.id).Take(screeShotPerThread);
                    data = dataLinq.ToList();

                    //Нет больше данных тест окончен.
                    if (data.Count == 0) break;

                    //Блокирую записи.
                    foreach (mTableWebicons m in data)
                    {
                        m.status = 1; //Говорю остальным потока-я работаю с этими данными.
                    }

                }

                //Формирую строку get запроса.
                string request = createURLString(data,"");
                stopWatch.Start();
                string jsonAnswer = sendGet(request);

                //Обработка исключений.
                while (jsonAnswer == null)
                {

                    Console.WriteLine("Thread" + threadNum.ToString() + "Exception  ");
                    Thread.Sleep(500);
                    jsonAnswer = sendGet(request);

                }


                stopWatch.Stop();

                TimeSpan ts = stopWatch.Elapsed;
                double s = ts.TotalSeconds;
                string elapsedTime = s.ToString("0.00");

                //Сохраняет результаты в БД.
                saveResults(elapsedTime, jsonAnswer, request);

                Console.WriteLine("Thread" + threadNum.ToString() + "  EndId=" + data[screeShotPerThread - 1].id.ToString());

                // Thread.Sleep(500);

            }


        }


        /// <summary>
        /// Поток выполняющий тестирование сервиса по новому алгоритму.
        /// </summary>
        private void taskTest1(int threadNum,int screeShotPerThread,string hostName,int startpos)
        {
            List<mTableWebicons> data;
            bool work = true;
            Stopwatch stopWatch = new Stopwatch(); //Измеряем время выполнения сервером запроса.

            int pos = startpos; //Количество обработанных.

            while (work)
            {
                   //Получаю первые screeShotPerThread не обработанных url.
                    IEnumerable<mTableWebicons> dataLinq = 
                    urls.Where(x => x.status == 0).OrderBy(x => x.id).Take(screeShotPerThread);
                    data = dataLinq.ToList();

                    //Нет больше данных тест окончен.
                    if (data.Count == 0) break;

                    //Блокирую записи.
                    foreach (mTableWebicons m in data)
                    {
                        m.status = 1; //Говорю остальным потокам-я работаю с этими данными.
                    }

                    pos+= data.Count; //Количество новых объектов для обработки.

                    
                //Формирую строку get запроса.
                string request = createURLString(data,hostName);

                stopWatch.Reset(); //Замер времени выполнения запроса.
                stopWatch.Start();

                Log.Information("Send request");

                string jsonAnswer = sendGet(request);

                //Обработка исключений.
                while (jsonAnswer == null)
                {
                    String msg = "Thread " + threadNum.ToString() + "Exception  ";
                    Thread.Sleep(500);
                    jsonAnswer = sendGet(request);

                }


                stopWatch.Stop();
                Log.Information(" End request");


                TimeSpan ts = stopWatch.Elapsed;
                double s = ts.TotalSeconds;
                string elapsedTime = s.ToString("N2");

                //Сохраняет результаты в БД.
                saveResults(elapsedTime, jsonAnswer, request);

                Log.Information("Thread" + threadNum.ToString() + " completed " +
                    pos.ToString() + " line. " + screeShotPerThread.ToString() + " screens " + elapsedTime + " second."); ;

            }


        }


        /// <summary>
        /// Формирую параметры для get запроса.
        /// </summary>
        private string createURLString(List<mTableWebicons> data, string hostName)
        {
            string getStr = hostName+ "/?"; 
            
            int cnt = 0;
   

            foreach (mTableWebicons m in data)
            {
                getStr += "url[" + cnt.ToString() + "]=";
                getStr +=HttpUtility.UrlEncode(m.url);
                getStr += "&";
                              
                cnt++;
            }
            getStr += "allowedReferer=1";

            return getStr;
        }

        /// <summary>
        /// Отправляет GET запрос с параметрами.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        string sendGet(string uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                mTableTestErrors err = new mTableTestErrors();
                err.url = uri;
                err.error = "Exception in  sendGet:" + ex.Message;
                err.create = DateTime.Now;

                string msg = err.error + " ;URl = " + uri;
                Log.Error(msg);

                lock (lockDb)
                {
                    db.TestErrors.Add(err);
                    db.SaveChanges();
                }

                return null;

            }

        }




        /// <summary>
        /// Сохраняет результаты в БД.
        /// </summary>
        /// <param name="elapsedTime"></param>
        /// <param name="ret"></param>
        void saveResults(string elapsedTime, string jsonAnswer, string url)
        {

            List<mRetJson> ret=null;
            try
            {
                ret = JsonSerializer.Deserialize<List<mRetJson>>(jsonAnswer);

                //Сохранения результатов.
                foreach (mRetJson m in ret)
                {
                    if (m.status == 1)
                    {
                        mTableTestResults r = new mTableTestResults();
                        r.elapsedTime = elapsedTime;
                        r.url = m.url;
                        r.response = m.path;
                        r.create = DateTime.Now;

                        lock (lockDb)
                        {
                            db.TestResults.Add(r);
                        }

                    }

                }


                //Сохранения ошибок.
                foreach (mRetJson m in ret)
                {
                    if (m.status == 0)
                    {
                        mTableTestErrors err = new mTableTestErrors();
                        err.url = m.url;
                        err.error = m.log;
                        err.create = DateTime.Now;
                        lock (lockDb)
                        {
                            db.TestErrors.Add(err);
                        }

                    }

                }

                lock (lockDb)
                {
                    db.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                string mess = "Exeption to save results:" + ex.Message;
                Console.WriteLine(mess);
                Console.WriteLine("jsonAnswer="+jsonAnswer);
                Console.WriteLine("url=" + url);
                mTableTestErrors err = new mTableTestErrors();
                err.url = url;
                err.error = jsonAnswer;
                err.create = DateTime.Now;
                lock (lockDb)
                {
                    db.TestErrors.Add(err);
                }

            }            

        }


    }
}
