using Microsoft.EntityFrameworkCore;
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
        object locker = new object();//Объект для блокировки общиго ресурса.
        object lockDb = new object();//Объект для блокировки доступа к БД.

        //Количество скриншотов выполняемые одним потоком за раз.
        private int screeShotPerThread = 5;

        dbContext db = new dbContext();



        public Tests()
        {
            db.Database.Migrate();
        }


        /// <summary>
        /// Один поток и большие запросы.
        /// </summary>
        public void Test1()
        {
            Console.WriteLine("Read file.");

            // prepareRundomFile();
             readFile2(ref urls);
             taskTest1(1);



        }





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



        void prepareRundomFile()
        {
            List<mTableWebicons> tmp = new List<mTableWebicons>();
            readFile1(ref tmp);

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


        public void readFile2(ref List<mTableWebicons> urls)
        {
            int id = 0;

            foreach (string line in System.IO.File.ReadLines(@"links.txt"))
            {

                    mTableWebicons m = new mTableWebicons();
                    m.url = line.Trim();
                    m.id = id;
                    id++;

                    urls.Add(m);

                
            }
        }




        /// <summary>
        /// Читает файл с http запросами. Вида
        /// INSERT INTO `websites_online` (`element_url`) VALUES
        // (' https://yandex.ru/?clid=2299450'),
        ///('http:// Angryfox.info'),
        /// </summary>
        /// <param name="urls"></param>
        public void readFile1(ref List<mTableWebicons> urls)
        {
            int id = 0;

            // Read the file and display it line by line.  
            bool enableBlock = false;
            foreach (string line in System.IO.File.ReadLines(@"websites_online.sql"))
            {

                //Начало вставки в таблицу.
                if (line == "INSERT INTO `websites_online` (`element_url`) VALUES")
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
                    m.url = arr[0].Replace("'", "");
                    m.url = m.url.Replace("(", "");
                    m.url = m.url.Replace(")", "");

                    m.url = m.url.Trim();
                    m.id = id;
                    id++;

                    urls.Add(m);

                }

            }

        }



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

                lock (locker)
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
                string request = createURLString(data);
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
                saveResults(s, jsonAnswer, request);


                Console.WriteLine("Thread" + threadNum.ToString() + "  EndId=" + data[screeShotPerThread - 1].id.ToString());

                // Thread.Sleep(500);

            }


        }


        /// <summary>
        /// Поток выполняющий тестирование сервиса по новому алгоритму.
        /// </summary>
        private void taskTest1(int threadNum)
        {
            List<mTableWebicons> data;
            bool work = true;
            Stopwatch stopWatch = new Stopwatch(); //Измеряем время выполнения сервером запроса.

            int pos = 0; //Количество обработанных.

            while (work)
            {
                   //Получаю первые screeShotPerThread не обработанных url.
                    IEnumerable<mTableWebicons> dataLinq = urls.Where(x => x.status == 0).OrderBy(x => x.id).Take(10);
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
                string request = createURLString(data);

                stopWatch.Reset();
                stopWatch.Start();
                Console.Write(DateTime.Now);
                Console.WriteLine("Send request");

                string jsonAnswer = sendGet(request);

                //Обработка исключений.
                while (jsonAnswer == null)
                {

                    Console.WriteLine("Thread " + threadNum.ToString() + "Exception  ");
                    Thread.Sleep(500);
                    jsonAnswer = sendGet(request);

                }


                stopWatch.Stop();
                Console.Write(DateTime.Now);
                Console.WriteLine(" End request");


                TimeSpan ts = stopWatch.Elapsed;
                double s = ts.TotalSeconds;
                string elapsedTime = s.ToString("0.00");

                //Сохраняет результаты в БД.
                saveResults(s, jsonAnswer, request);


                Console.WriteLine("Thread" + threadNum.ToString() + " curPos="+pos.ToString());

            }


        }


        /// <summary>
        /// Формирую параметры для get запроса.
        /// </summary>
        private string createURLString(List<mTableWebicons> data)
        {
            string getStr = "http://192.168.195.129:5000/?";
            //string getStr = "http://localhost:5000/?"; //Linux Host.
            //string getStr = "https://localhost:44350/?";

            //http://192.168.195.129:5000/?url[0]=https://google.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1
            int cnt = 0;
            //Нужно кодировать запрос,так как есть символ #.
            bool needEncoding = false;

            //Всегда кодировать спец символы.
            needEncoding = true;

            //Проверка наличия символа # в url.
            /*
            foreach (mTableWebicons m in data)
            {
                if (m.url.Contains('#'))
                {
                    needEncoding = true;
                    break;
                }
            }
            */

            foreach (mTableWebicons m in data)
            {
                //getStr += "url[" + cnt.ToString() + "]=https://" + m.url + "&";
                getStr += "url[" + cnt.ToString() + "]=";

                if (needEncoding)
                {
                    getStr +=HttpUtility.UrlEncode(m.url);
                }

                getStr += "&";
                              

                cnt++;
            }
            getStr += "allowedReferer=1";

            //if (needEncoding)
             //   getStr += "&useEncoding=1";

            return getStr;
        }


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
                Console.WriteLine("URl="+uri);
                Console.WriteLine(err.error);

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
        void saveResults(double elapsedTime, string jsonAnswer, string url)
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
