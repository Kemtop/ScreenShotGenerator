
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ScreenShotGenerator.Services.ScreenShoterLogic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    public class ImpBrowserControlChrome : IBrowserControl
    {
        /// <summary>
        /// Объект для управления браузером.
        /// </summary>
        IWebDriver Browser;

        //Пул задач.
        poolTasks poolTasks;
        /// <summary>
        /// Разрешен запуск потока. Флаг используется для остановки потока.
        /// </summary>
        private bool threadIsRun = true;

        //Синхронизация потоков.
        object locker;

        /// <summary>
        /// Количество задач из пула которые браузер обрабатывает за раз.
        /// </summary>
        public int tasksPerThread { get; set; }

        //Директория для хранения картинок.
        private string tmpDir;


        /// <summary>
        /// Обработка задач в потоке задач. Выполняется запускает отдельный процесс для проверки и обработки задачи.
        /// </summary>
        /// <param name="poolTasks"></param>
        public void processPool(ref poolTasks pool, ref object locker)
        {
            this.poolTasks = pool;
            tmpDir = pool.tmpDir; 
            this.locker = locker;
            Thread thread = new Thread(processPoolThread);
            thread.Start();

            /* Почитай про правильную многопоточность.
             * https://stackoverflow.com/questions/8014037/c-sharp-call-a-method-in-a-new-thread
             * Action secondFooAsync = new Action(SecondFoo);

secondFooAsync.BeginInvoke(new AsyncCallback(result =>
      {
         (result.AsyncState as Action).EndInvoke(result); 

      }), secondFooAsync); 
             */

            // throw new NotImplementedException();
        }

        /// <summary>
        /// Запуск браузера.
        /// </summary>
        public void startBrowser()
        {
            runBrowser();
        }


        /// <summary>
        ///Остановка браузера.
        /// </summary>
        public void stopBrowser()
        {
            threadIsRun = false; //Остановка процесса обработки задач, если запущен.
            Browser.Quit();
        }


        private void processPoolThread()
        {
            while (threadIsRun)
            {
                //Список задач из пула.
                List<mJobPool> data;

                //Блокирую пул для других потоков.
                lock (locker)
                {
                    data = poolTasks.getNeedProcessing(tasksPerThread);

                    //Есть новые задачи.
                    if(data.Count>0)
                    {
                        //Блокирует для обработки. Другие потоки не будут обращать внимания на данные объекты.
                        foreach (mJobPool p in data) p.status = 1;
                    }

                }


                if (data.Count == 0) //Нет данных для обработки.
                {
                    Thread.Sleep(1000);
                    continue;
                }


                foreach (mJobPool p in data)
                {
                    p.fileName = getMD5(p.url) + ".jpg"; //Формирую имя файла.
                    //Cоздание скриншота.
                    string err=takeScreenShot(p.url, p.fileName);

                    p.timestamp = DateTime.Now;

                    bool allGood = true; //Нет ошибок в процессе работы.

                    //Ошибка создания скрин шота.
                    if(err!=null)
                    {
                        p.path = err;
                        allGood = false;
                    }
                    else
                    {
                        //Почему то файл не создался. 
                        if (!checkExistFile(p.fileName))
                        {
                            p.path = "File no exist.";
                            allGood = false;
                        }


                        //Почему то файл пуст. 
                        if (!checkFileSize(p.fileName))
                        {
                            p.path = "File length is 0.";
                            allGood = false;
                        }

                    }                  

                                       

                    //Были ли ошибки?
                    if (allGood)
                    p.status = 3; //Все хорошо.
                    else
                     p.status = 2;


                }



            }
        }


        /// <summary>
        /// Проверяю существования файла в папке кеша.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool checkExistFile(string fileName)
        {            
            var path = Path.Combine("wwwroot/"+tmpDir, fileName);
            bool exists = System.IO.File.Exists(path);
            return exists;
        }

        /// <summary>
        /// Проверяю размер файла.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool checkFileSize(string fileName)
        {
            var path = Path.Combine("wwwroot/" + tmpDir, fileName);
            long length = new System.IO.FileInfo(path).Length;

            if (length == 0) return false;

            return true;
        }

       



        /// <summary>
        /// На основании входной строки формирует ее хеш
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string getMD5(String input)
        {

            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                return string.Join("", result.Select(x => x.ToString("X2"))).ToLower();
            }

        }


        private void runBrowser()
        {

            try
            {

                var chromeOptions = new ChromeOptions();
                //Работаем на сервере без видеокарты.
                chromeOptions.AddArgument("--disable-gpu");

                //Опции из index.php.
                //--window-size=1280,1060 //вынесено выше в другую логику.
                chromeOptions.AddArgument("--window-size=1920,1080");
                //локальная папка для браузера.профиль.не работает на винде,ни чего не открывает.
                //На linux сжирает в два раза больше памяти.
                // chromeOptions.AddArgument("--user-data-dir=usr_dir"); 
                chromeOptions.AddArgument("--window-position=0,0");//
                //--display=:$DISP думаю пока не нужно, так как в окружении службы задано.
                //chromeOptions.AddArgument("--window-size=768x1024");
                chromeOptions.AddArgument("--incognito");// 
                chromeOptions.AddArgument("--disable-cache");// 
                chromeOptions.AddArgument("--disable-component-update");// ";
                chromeOptions.AddArgument("--disable-desktop-notifications");
                chromeOptions.AddArgument("--disable-translate");
                // chromeOptions.AddArgument("--enable-download-notification"); //не знаю за чем это.

                //" \"$url\" & sleep ".($timeout * ($i + 1)).
                //" && DISPLAY=:$DISP gm import -window root -crop 1260x965-0+60 -resize 300 $screen_path";

                Browser = new OpenQA.Selenium.Chrome.ChromeDriver(chromeOptions);

                //chromeOptions.AddArgument("---disable-gpu");
                //chromeOptions.AddArgument("start-maximized"); // open Browser in maximized mode
                //chromeOptions.AddArgument("disable-infobars"); // disabling infobars
                // chromeOptions.AddArgument("--disable-extensions"); // disabling extensions
                //chromeOptions.AddArgument("--no-sandbox");
                //chromeOptions.AddArgument("--disable-setuid-sandbox");

                //chromeOptions.AddArgument("--disable-dev-shm-using");
                //chromeOptions.AddArgument("--disable-extensions");

                //chromeOptions.AddArgument("start-maximized"); иначе ошибка
                //chromeOptions.AddArgument("disable-infobars");
                //chromeOptions.AddArgument("--user-data-dir");

                // chromeOptions.AddArgument("--disable-gpu"); // applicable to windows os only
                // chromeOptions.AddArgument("--disable-dev-shm-usage"); // overcome limited resource problems
                // chromeOptions.AddArgument("--no-sandbox"); // Bypass OS security model
                //chromeOptions.AddArgument("--remote-debugging-port=9222"); // Bypass OS security model

                /*
                 * System.setProperty("webdriver.chrome.driver", "C:\\path\\to\\chromedriver.exe");
ChromeOptions options = new ChromeOptions();
options.addArguments("start-maximized"); // open Browser in maximized mode
options.addArguments("disable-infobars"); // disabling infobars
options.addArguments("--disable-extensions"); // disabling extensions
options.addArguments("--disable-gpu"); // applicable to windows os only
options.addArguments("--disable-dev-shm-usage"); // overcome limited resource problems
options.addArguments("--no-sandbox"); // Bypass OS security model
WebDriver driver = new ChromeDriver(options);
driver.get("https://google.com");
                 */


                //--disable-cache --disable-component-update --disable-desktop-notifications --disable-translate
                //--disable-dev-shm-usage
                // chromeOptions.AddArguments("window-size=1280,1060");


                // chromeOptions.AddArgument("--log-level=1");
                // chromeOptions.AddArgument("--enable-logging --v=1");


                /*
                 * Включение лога отладки ChromeDriverService, очень помогло! Не удаляй.
                var service = ChromeDriverService.CreateDefaultService();
                service.LogPath = "chromedriver.log";
                service.EnableVerboseLogging = true;
                Browser = new ChromeDriver(service);
                */

                //Установка размера.
                Browser.Manage().Window.Position = new System.Drawing.Point(0, 0); ;
                //Browser.Manage().Window.Size = new Size(1024, 768);

                //Работает но странно.
                Browser.Manage().Window.Size = new System.Drawing.Size(1280, 1060);
                //Browser.Manage().Window.FullScreen();// Maximize(); //Разворачиваем браузер на весь экран.
                //Browser.Manage().Window.Minimize();
                //Browser.Manage().Window.Size = new Size(480,320);
                //logger.LogInformation(Browser.Manage().Window.Size.ToString());

            }
            catch (Exception ex)
            {
                //  MessageBox.Show(ex.Message);
                //Исключение если не верная версия браузера.
                throw new Exception("Exeption on metod runBrowser(user=" + Environment.UserName + "):" + ex.Message);
            }

        }



        /// <summary>
        /// Создает скрин шот, в случае ошибок возвращает строку.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private string takeScreenShot(string url, string filename)
        {
            try
            {

                //Browser.Manage().Window.Size = new Size(1280, 1060);
                //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
                Browser.Navigate().GoToUrl(url);
                string bodyText =Browser.FindElement(By.TagName("body")).Text;

                //Обработка ошибки 404.
                if(bodyText.Contains("404"))
                {
                    return "Error 404 in body:" + bodyText;
                }


                /*
                bool wait = new WebDriverWait(Browser, TimeSpan.FromSeconds(60)).Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

                if (wait == true)
                {
                    //Your code
                }
                */
                // Thread.Sleep(2000);

                /*
                Screenshot ss = ((ITakesScreenshot)Browser).GetScreenshot();
                ss.SaveAsFile("screen" + DateTime.Now.ToString("hh_mm_ss_fff") + ".jpg");
                */
                // Take Screenshot

                var screenshot = ((ITakesScreenshot)Browser).GetScreenshot();

                // Build an Image out of the Screenshot
                //Image screenshotImage;
                //Image img = System.Drawing.Image.FromStream(myStream);


                //screenshotImage = Image.FromStream(memStream);

                using (var stream = new MemoryStream())
                {
                    using var image = Image.Load(screenshot.AsByteArray);
                    image.Mutate(x => x
                        //.AutoOrient() // this is the important thing that needed adding
                        .Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Crop,
                            Position = AnchorPositionMode.Center,
                            Size = new SixLabors.ImageSharp.Size(1260, 965)
                        })
                        .BackgroundColor(SixLabors.ImageSharp.Color.White));


                    //var fileName = Path.GetFileName("screen" + DateTime.Now.ToString("hh_mm_ss_fff") + ".jpg");
                    var fileName = Path.GetFileName(filename);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot/" + tmpDir, fileName);

                    image.Save(filePath, new JpegEncoder() { Quality = 85 });

                    //  image.Save("screen" + DateTime.Now.ToString("hh_mm_ss_fff") + ".jpg",
                    //        new JpegEncoder() { Quality = 85 });




                }
            }catch(Exception ex)
            {
                return "Exception in metod takeScreenShot:" + ex.Message;
            }

            return null;

        }


    }
}
