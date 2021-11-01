
using ImageMagick;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ScreenShotGenerator.Services.ScreenShoterLogic;
using Serilog;
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
        private bool threadIsRun;

        //Синхронизация потоков.
        object locker;

        /// <summary>
        /// Количество задач из пула которые браузер обрабатывает за раз.
        /// </summary>
        public int tasksPerThread { get; set; }

        //Директория для хранения картинок.
        private string tmpDir;

        //Идентификатор браузера.
        private int browserId=0;

        /// <summary>
        /// Задача выборки данных из пула и их обработки.
        /// </summary>
        // private Task workTask;
        private Thread workThread;


        /// <summary>
        /// Делегат для сохранения сведений об ошибках браузера.
        /// </summary>
        private saveBrowserError saveBrowserErrorDg;

        /// <summary>
        /// Тайм аут загрузки страницы.
        /// </summary>
        private int pageLoadTimeouts;
        /// <summary>
        /// Тайм аут загрузки скрипта.
        /// </summary>
        private int javaScriptTimeouts;

        /// <summary>
        /// Путь к текущей папке.
        /// </summary>
        private string curentDirectory;


        public ImpBrowserControlChrome()
        {
            //Путь к рабочей директории приложения.
            curentDirectory = Directory.GetCurrentDirectory();
        }
           

        /// <summary>
        /// Задает идентификатор потока.
        /// </summary>
        /// <param name="id"></param>
       public void setTaskId(int id)
        {
            browserId = id;
        }

        /// <summary>
        /// Обработка задач в потоке задач. Выполняется запускает отдельный процесс для проверки и обработки задачи.
        /// </summary>
        /// <param name="poolTasks"></param>
        public void processPool(ref poolTasks pool, ref object locker, saveBrowserError saveBrowserErrorDg_)
        {
            this.poolTasks = pool;
            tmpDir = pool.tmpDir; 
            this.locker = locker;
            saveBrowserErrorDg = saveBrowserErrorDg_;

            threadIsRun = true; //Задача может работать.
                                //Запускаю задачу.
                                // workTask = new Task(processPoolThread);
                                // workTask.Start();
            workThread = new Thread(processPoolThread);
            workThread.Start();


            /* Почитай про правильную многопоточность.
             * https://stackoverflow.com/questions/8014037/c-sharp-call-a-method-in-a-new-thread
             * Action secondFooAsync = new Action(SecondFoo);
             */
        }

        /// <summary>
        /// Запуск браузера.
        /// </summary>
        public void startBrowser()
        {
            runBrowser();
        }


        /// <summary>
        ///Отстанавливает браузер,завершает задачу.
        /// </summary>
        public void stopProcess()
        {
            threadIsRun = false; //Остановка процесса обработки задач, если запущен.
            Browser.Quit();
            //Ждем завершения потока.
            workThread.Join();
            //Task.WaitAny(workTask);
        }


        private void processPoolThread()
        {
            while (threadIsRun)
            {
                //Список задач из пула.
                List<mJobPool> data=null; 

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


                //Пул заблокирован или нет данных для обработки.
                if ((data==null)||(data.Count == 0)) 
                {
                    //Сервис останавливают. Выходим.
                    if (!threadIsRun) return;
                    Thread.Sleep(1000);
                    continue;
                }


                foreach (mJobPool p in data)
                {                    
                    //Сервис останавливают. Выходим.
                    if (!threadIsRun) return;

                    //Проверка урл на пустоту.
                    if(String.IsNullOrEmpty(p.url))
                    {
                        string errMsg = "Error:Empty url!";
                        p.status = 2;
                        p.fileName = errMsg;
                        //Сохраняю логи в БД.
                        saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);                        
                        continue;
                    }


                    String lastError = null; //Последнее сообщение об ошибке, если есть.
                    p.fileName = getMD5(p.url) + ".jpg"; //Формирую имя файла.
                    //Путь куда сохранять файл.
                    string filePath = Path.Combine("wwwroot/" + tmpDir, p.fileName);
                
                   
                    //Cоздание скриншота.
                   // Log.Information("take "+p.url+";Browser="+browserId.ToString());
                    string err=takeScreenShot(p.url, filePath,p.fileName);
                    //Log.Information("end " + p.url + ";Browser=" + browserId.ToString());

                    //Сервис останавливают. Выходим. Браузер мог вообще упасть и вернуть сообщение об ошибке.
                    if (!threadIsRun) return;

                    p.timestamp = DateTime.Now;

                    bool allGood = true; //Нет ошибок в процессе работы.

                    //Ошибка создания скрин шота.
                    if(err!=null)
                    {
                        lastError= err;
                        allGood = false;
                    }
                    else
                    {                        
                        // Проверяет итоговый файл на существование, на размер,
                        // и на заполнение только белым или только черным.
                        allGood = checkResultFile(out lastError,filePath);
                    }                  
                 
                    //Пока пул не будет доступен. Или поток не остановят.
                    while(threadIsRun)
                    {
                        lock(locker)
                        {
                            //Были ли ошибки?
                            if (allGood)
                                p.status = 3; //Все хорошо.
                            else
                            {
                                p.status = 2;
                                p.fileName = lastError;
                                //Сохраняю логи в БД.
                                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError,lastError, p.url, p.fileName);
                            }
                                

                            break;
                        }

                        Task.Delay(300);
                    }    

                  
                }
            }
        }

        /// <summary>
        /// Проверяет итоговый файл на существование, на размер, и на заполнение только белым или только черным.
        /// Если все хорошо=true.
        /// </summary>
        /// <param name="errMess"></param>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        private bool checkResultFile(out string errMess,string pathToFile)
        {
            errMess = null;

            //Почему то файл не создался. 
            if (!checkExistFile(pathToFile))
            {
                errMess = "File no exist.";
                return false;
            }


            //Почему то файл пуст.
            if (!checkFileSize(pathToFile))
            {
                errMess = "File length is 0.";
                return false;
            }

            //Проверяет не вернул ли браузер черную или белую картинку.
            int chkColorErr = imgOnlyBlackOrWhite(pathToFile);
            if (chkColorErr != 0)
            {
                errMess = "Image contains only " + ((chkColorErr == 1) ? "white" : "black") + " pixels.";
                return false;
            }

            return true;
        }


        /// <summary>
        /// Проверяю существования файла в папке кеша.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool checkExistFile(string path)
        {            
            bool exists = System.IO.File.Exists(path);
            return exists;
        }

        /// <summary>
        /// Проверяю размер файла.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool checkFileSize(string path)
        {

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
                chromeOptions.AddArgument("--enable-download-notification");


          


                /*
                 * В Chrome «Разрешить ограничения на загрузку» есть 4 варианта:
                    0 = нет особых ограничений
                    1 = блокировать опасные загрузки
                    2 = блокировать потенциально опасные загрузки
                    3 = заблокировать все загрузки
                    4 = блокировать вредоносные загрузки
                 */
                //Отключить загрузку файлов.              
                chromeOptions.AddArgument("--disable-infobars");
                chromeOptions.AddUserProfilePreference("download_restrictions" , 3);
                


                /*
                 * driverOptions.AddUserProfilePreference("download.default_directory", BaseCommon._chromeDefaultDownloadsFolder);
driverOptions.AddUserProfilePreference("intl.accept_languages", "nl");
driverOptions.AddUserProfilePreference("profile.default_content_settings.popups", "0");
driverOptions.AddUserProfilePreference("disable-popup-blocking", "true");
var driverPath = System.IO.Directory.GetCurrentDirectory();
Instance = new ChromeDriver(driverPath, driverOptions);
                chromeOptions.AddUserProfilePreference("download.default_directory", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
chromeOptions.AddUserProfilePreference("disable-popup-blocking", "true");
chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
chromeOptions.AddUserProfilePreference("safebrowsing.enabled", true);
                 */







                //" \"$url\" & sleep ".($timeout * ($i + 1)).
                //" && DISPLAY=:$DISP gm import -window root -crop 1260x965-0+60 -resize 300 $screen_path";

                Browser = new OpenQA.Selenium.Chrome.ChromeDriver(chromeOptions);

                //В процессе тестов встретились сайты загрузка которых "крутиться" более минуты, что приводит
                //к тайм ауту взаимодействия с драйвером. Исключаем такую ситуацию.
                Browser.Manage().Timeouts().PageLoad=TimeSpan.FromSeconds(pageLoadTimeouts);
                Browser.Manage().Timeouts().AsynchronousJavaScript=TimeSpan.FromSeconds(javaScriptTimeouts);

          


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
        private string takeScreenShot(string url, string filePath,string filename)
        {
           
                //Выполняю проверку живой ли браузер.
                try
                {
                     //Если с объектом что то не то-думаю должно высыпаться. Но как проверить пока не ясно.
                    string ttl = Browser.Title;

                    if(ttl==null)
                    {
                        saveBrowserErrorDg((int)enumBrowserError.Debug, "Warning! Browser title is null. May be crash?",url,filename);
                    }

                }
                catch(Exception ex)
                {
                    string str = "Exception to check title. Browser may be dead.: " + ex.Message;
                    saveBrowserErrorDg((int)enumBrowserError.ProblemWithBrowser, str, url, filename); 
                    return "Error 701";
                }
                

            try
            {
                //Browser.Manage().Window.Size = new Size(1280, 1060);
                //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
                Browser.Navigate().GoToUrl(url);
            }
            catch(Exception ex)
            {
                string str = "Exception to GoToUrl: " + ex.Message;
                saveBrowserErrorDg((int)enumBrowserError.GoUrl, str, url, filename);
                //Обработали исключение, сделали скрин шот, отправили пользователю.
            }

              


            try
            {
                /*
            string bodyText =Browser.FindElement(By.TagName("body")).Text;
            //Обработка ошибки 404.
            if(bodyText.Contains("404"))
            {
                return "Error 404 in body:" + bodyText;
            }
            */


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
                    // var fileName = Path.GetFileName(filename);
                    // var filePathFull = Path.Combine(curentDirectory, @"wwwroot/" + tmpDir, fileName);
                    string filePathFull = Path.Combine(curentDirectory,filePath);


                    image.Save(filePathFull, new JpegEncoder() { Quality = 85 });

                    //  image.Save("screen" + DateTime.Now.ToString("hh_mm_ss_fff") + ".jpg",
                    //        new JpegEncoder() { Quality = 85 });


                }
            }catch(Exception ex)
            {
                String str= "Exception in metod takeScreenShot where create and save screenshot: " + ex.Message;
                saveBrowserErrorDg((int)enumBrowserError.ProblemWithBrowser, str, url, filename);
                return "Error 702";
            }

            return null;

        }

        /*
         * Для тестов.
         *  String str1 = Path.Combine(Directory.GetCurrentDirectory(), "FullWhite.png");
            String str2 = Path.Combine(Directory.GetCurrentDirectory(), "FullBlack.png");
            String str3 = Path.Combine(Directory.GetCurrentDirectory(), "helloMan.jpg");

            int y1 = imgOnlyBlackOrWhite(str1);
            int y2 = imgOnlyBlackOrWhite(str2);
            int y3 = imgOnlyBlackOrWhite(str3);
            y1 = 10;
         */

        /// <summary>
        /// Проверет содержит ли картинка только черные или белые пиксели.
        /// Т.е. рисунок полностью белый или полностью черный.
        /// Если только белые=1, если только черные=2, нет однотонных =0
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private int imgOnlyBlackOrWhite(string path)
        {
            //Думаю что вся картинка содержит черные пиксели.
            bool onlyWhite = true;
            bool onlyBlack = true;

            using (var image = new MagickImage(path))
            {
                MagickColor white = MagickColors.White;
                MagickColor black = MagickColors.Black;

                using (IPixelCollection<ushort> pixels = image.GetPixels())
                {
                    //Проверка наличия только белых пикселей.
                    foreach (var pixel in pixels)
                    {
                        IMagickColor<ushort> color = pixel.ToColor();
                        if (!((color.R == white.R) && (color.G == white.G) && (color.B == white.B) &&
                            (color.A == white.A) && (color.K == white.K)))
                        {
                            onlyWhite = false;
                            break;
                        }

                    }

                    if (onlyWhite) return 1; //Только белый.

                    //Проверка наличия только черных пикселей.
                    foreach (var pixel in pixels)
                    {
                        IMagickColor<ushort> color = pixel.ToColor();
                        if (!((color.R == black.R) && (color.G == black.G) && (color.B == black.B) &&
                           (color.A == black.A) && (color.K == black.K)))
                        {
                            onlyBlack = false;
                            break;
                        }

                    }

                    if (onlyBlack) return 2; //Только черный.

                }

            }

            return 0;
        }

        /// <summary>
        /// Задает тайм ауты.
        /// </summary>
        /// <param name="pageLoadTimeouts"></param>
        /// <param name="javaScriptTimeouts"></param>
        public void setTimeouts(int pageLoadTimeouts, int javaScriptTimeouts)
        {
            this.pageLoadTimeouts = pageLoadTimeouts;
            this.javaScriptTimeouts = javaScriptTimeouts;
        }


        /*
        /// <summary>
        /// Создает стандартную исконку с надписью.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fileName"></param>
        private void createErrorImage(string url,string fileName)
        {
        using ImageMagick;
         var pathToBackgroundImage = "helloMan.jpg";
            var pathToNewImage = "helloMan1.jpg";
            var textToWrite = "Text";

            // These settings will create a new caption
            // which automatically resizes the text to best
            // fit within the box.

            var readSettings = new MagickReadSettings
            {
                Font = "Calibri",
                FontPointsize=50,
                TextGravity = Gravity.Center,
                BackgroundColor = MagickColors.Transparent,
                FillColor = MagickColors.Red, // -fill black
                StrokeColor = MagickColors.Red,                
                Height = 50, // height of text box
                Width = 400 // width of text box
            };

            //Размер картинки 600х597

            using (var image = new MagickImage(pathToBackgroundImage))
            {
                using (var caption = new MagickImage($"caption:{textToWrite}", readSettings))
                {
                    // Add the caption layer on top of the background image
                    // at position 590,450
                    image.Composite(caption, 20, 400, CompositeOperator.Over);

                    image.Write(pathToNewImage);
                }
            }
        }
        */

    }
}
