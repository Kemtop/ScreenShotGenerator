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



        public void run()
        {
            //Читаю данные.
            readFile(ref urls);
            //Начинаю тест с позиции 381.
            int i = 0;
            foreach(mTableWebicons m in urls)
            {
                if (i > 380) break;
                m.status = 1;
                i++;
            }

            Thread thread1 = new Thread(()=>taskTest(1));
            Thread thread2 = new Thread(() => taskTest(2));
            Thread thread3 = new Thread(() => taskTest(3));

            thread1.Start();
            thread2.Start();
            thread3.Start();

            Console.WriteLine("End");
            int y = 0;

        }

        public void readFile(ref List<mTableWebicons> urls)
        {
       

            // Read the file and display it line by line.  
            bool enableBlock = false;
            foreach (string line in System.IO.File.ReadLines(@"webicons_list.sql"))
            {

                //Начало вставки в таблицу.
                if(line== "INSERT INTO `webicons_list` (`id`, `host`, `last_up`, `is_create`) VALUES")
                { 
                    enableBlock = true;
                    continue;
                }
                   

                if (enableBlock&&(line.Contains(';')))
                {
                    enableBlock = false;
                }

                //Разрешено чтение блока.
                if(enableBlock)
                {
                    String[] arr = line.Split(',');
                    mTableWebicons m = new mTableWebicons();
                    m.id = Convert.ToInt32(arr[0].Replace('(',' '));
                    m.url = arr[1].Replace("'","");
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
                    foreach(mTableWebicons m in data)
                    {
                        m.status = 1; //Говорю остальным потока-я работаю с этими данными.
                    }

                }

                //Формирую строку get запроса.
                string request=createURLString(data);
                stopWatch.Start();
                string jsonAnswer = sendGet(request);

                //Обработка исключений.
                while(jsonAnswer==null)
                {

                    Console.WriteLine("Thread" + threadNum.ToString() + "Exception  ");
                    Thread.Sleep(500);
                    jsonAnswer = sendGet(request);

                }


                stopWatch.Stop();

                TimeSpan ts = stopWatch.Elapsed;
                 double s= ts.TotalSeconds;
                string elapsedTime = s.ToString("0.00");

                //Сохраняет результаты в БД.
                saveResults(s,jsonAnswer,request);

                                
                Console.WriteLine("Thread"+threadNum.ToString()+"  EndId="+data[screeShotPerThread-1].id.ToString());

               // Thread.Sleep(500);

            }

                       
        }

        /// <summary>
        /// Формирую параметры для get запроса.
        /// </summary>
        private string createURLString(List<mTableWebicons> data)
        {
            //string getStr = "http://192.168.195.129:5000/?";
            string getStr = "http://localhost:5000/?";


            //http://192.168.195.129:5000/?url[0]=https://google.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1
            int cnt = 0;
            foreach (mTableWebicons m in data)
            {
                getStr+= "url["+cnt.ToString()+"]=https://"+m.url+"&";
                cnt++;
            }
            getStr += "allowedReferer=1";

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
            catch(Exception ex)
            {
                mTableTestErrors err = new mTableTestErrors();
                err.url = uri;
                err.error = "Exception in  sendGet:"+ex.Message;
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
            mTableTestResults r = new mTableTestResults();
            r.elapsedTime = elapsedTime;
            r.url = url;
            r.response = jsonAnswer;
            lock(lockDb)
            {
                db.TestResults.Add(r);
            }


            List<mRetJson> ret = JsonSerializer.Deserialize<List<mRetJson>>(jsonAnswer);

            //Сохранения ошибок.
            foreach (mRetJson m in ret)
            {
                if(m.status==2)
                {
                    mTableTestErrors err = new mTableTestErrors();
                    err.url = m.url;
                    err.error = m.path;
                    lock(lockDb)
                    {
                        db.TestErrors.Add(err);
                    }
                    
                }

            }

            lock(lockDb)
            {
                db.SaveChanges();
            }
            

        }


    }
}
