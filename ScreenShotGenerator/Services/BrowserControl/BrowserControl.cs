using ImageMagick;
using OpenQA.Selenium;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterPools;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Логика управления браузером.
    /// </summary>
    public class BrowserControl
    {
        /// <summary>
        /// Объект для управления браузером(драйвер).
        /// </summary>
        private IBrowserControl Browser;

        //Пул задач.
        private poolTasks poolTasks;

        /// <summary>
        /// Разрешен запуск потока. Флаг используется для остановки потока.
        /// </summary>
        private bool threadIsRun;

        //Синхронизация потоков.
        private object lockPoolTasks;

        //Директория для хранения картинок.
        private string tmpDir;

        /// <summary>
        /// Задача выборки данных из пула и их обработки.
        /// </summary>
        // private Task workTask;
        //private Thread workThread;
        private Task workThread;

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

        /// <summary>
        /// Количество задач из пула которые браузер обрабатывает за раз.
        /// </summary>
        public int tasksPerThread { get; set; }

        /// <summary>
        /// Идентификатор браузера.
        /// </summary>
        public int browserId { get; set; }

        public BrowserControl(IBrowserControl Browser )
        {
            //Путь к рабочей директории приложения.
            curentDirectory = Directory.GetCurrentDirectory();
            this.Browser =Browser;
        }

        /// <summary>
        /// Обработка задач в потоке задач. Выполняется запускает отдельный процесс для проверки и обработки задачи.
        /// </summary>
        /// <param name="poolTasks"></param>
        public void processPool(ref poolTasks pool, ref object locker, saveBrowserError saveBrowserErrorDg_, string tmpDir)
        {
            this.poolTasks = pool;
            this.lockPoolTasks = locker;
            this.tmpDir = tmpDir;
            saveBrowserErrorDg = saveBrowserErrorDg_;

            threadIsRun = true; //Задача может работать.
                                //Запускаю задачу.
            workThread = new Task(processPoolThread);
            // workTask.Start();
            //workThread = new Thread(processPoolThread);
            workThread.Start();
        }


        /// <summary>
        ///Отстанавливает браузер,завершает задачу.
        /// </summary>
        public void stopProcess()
        {
            threadIsRun = false; //Остановка процесса обработки задач, если запущен.
            Browser.Quit();
            //Ждем завершения потока.
            //workThread.Join();
            //Task.WaitAny(workTask);
            Task.WaitAny(workThread);
        }


        /// <summary>
        /// Проверяет есть ли в пуле новые задачи, выполняет их.
        /// </summary>
        private void processPoolThread()
        {
            while (threadIsRun)
            {
                //Список задач из пула.
                List<mJobPool> data = null;

                //Блокирую пул для других потоков.
                lock (lockPoolTasks)
                {
                    data = poolTasks.getNeedProcessing(tasksPerThread);

                    //Есть новые задачи.
                    if (data.Count > 0)
                    {
                        //Блокирует для обработки. Другие потоки не будут обращать внимания на данные объекты.
                        foreach (mJobPool p in data)
                        {
                            p.status = (int)enumTaskStatus.LockByBrowser;
                            p.browserId = browserId;
                        }
                    }

                }


                //Пул заблокирован или нет данных для обработки.
                if ((data == null) || (data.Count == 0))
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
                    if (String.IsNullOrEmpty(p.url))
                    {
                        string errMsg = "Error:Empty url!";
                        p.status = (int)enumTaskStatus.Error;
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
                    string err = takeScreenShot(p.url, filePath, p.fileName, ref p.wastedTime);
                    //Log.Information("end " + p.url + ";Browser=" + browserId.ToString());

                    //Сервис останавливают. Выходим. Браузер мог вообще упасть и вернуть сообщение об ошибке.
                    if (!threadIsRun) return;

                    p.timestamp = DateTime.Now;

                    bool allGood = true; //Нет ошибок в процессе работы.

                    //Ошибка создания скрин шота.
                    if (err != null)
                    {
                        lastError = err;
                        allGood = false;
                    }
                    else
                    {
                        // Проверяет итоговый файл на существование, на размер,
                        // и на заполнение только белым или только черным.
                        allGood = checkResultFile(out lastError, filePath);
                    }

                    //Пока пул не будет доступен. Или поток не остановят.
                    while (threadIsRun)
                    {
                        lock (lockPoolTasks)
                        {
                            //Были ли ошибки?
                            if (allGood)
                                p.status = (int)enumTaskStatus.End; //Все хорошо.
                            else
                            {
                                p.status = (int)enumTaskStatus.Error;
                                p.fileName = lastError;
                                //Сохраняю логи в БД.
                                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, lastError, p.url, p.fileName);
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
        private bool checkResultFile(out string errMess, string pathToFile)
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


        /// <summary>
        /// Настраивает драйвер, и вызвает запуск браузера.
        /// </summary>
        private bool runBrowser()
        {
            try
            {


                //Отключить загрузку файлов.
                //firefox -p
                FirefoxOptions options = new FirefoxOptions();
                FirefoxProfile profile = new FirefoxProfileManager().GetProfile("/home/screenShotService/site/4h9zjw8h.user");
                if (profile == null)
                {
                    Log.Information("null profile");
                    // options.SetPreference("javascript.enabled",false);
                    //Долждна быть установлена версия дравера 0.30.0.1 иначе работать не будет.
                    options.SetPreference("webgl.disabled", true);
                    //browser.privatebrowsing.autostart

                }
                else
                {
                    options.Profile = profile;
                }



                Browser = new FirefoxDriver(options);
                Log.Information("io");

                // Browser = new OpenQA.Selenium.Chrome.
                //   TimeSpan.FromSeconds(8)); //Время ожидания ответа от  WebDriverа.
                //Дабы исключить ошибку вида: The HTTP request to the remote WebDriver server for URL
                //http://localhost:38445/session/6fd13ff4c79b9ae2993d94f9c58499d0/url timed out after 60 seconds.

                // actions = new Actions(Browser);

                //В процессе тестов встретились сайты загрузка которых "крутиться" более минуты, что приводит
                //к тайм ауту взаимодействия с драйвером. Исключаем такую ситуацию.
                Browser.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(pageLoadTimeouts);
                Browser.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(javaScriptTimeouts);

                //Установка размера.
                Browser.Manage().Window.Position = new System.Drawing.Point(0, 0); ;
                Browser.Manage().Window.Size = new System.Drawing.Size(1280, 1060);


            }
            catch (Exception ex)
            {
                //Исключение если не верная версия браузера.
                String msg = "Exeption on metod runBrowser(user=" + Environment.UserName + "):" + ex.Message;
                Log.Error(msg);
                return false;
            }

            return true;
        }


        int takeScreen = 0;
        private void stopLoadPage()
        {

            int max = pageLoadTimeouts * 100;
            int cnt = 0;
            while (takeScreen == 0)
            {
                if (cnt > max) break;
                Task.Delay(10);
                cnt++;
            }
            if (takeScreen == 1) return;
           // actions.SendKeys(Keys.Escape);
           // Log.Information("stopLoadPage");
        }


        /// <summary>
        /// Создает скрин шот, в случае ошибок возвращает строку.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private string takeScreenShot(string url, string filePath, string filename, ref float elipsedTime)
        {


            //Выполняю проверку живой ли браузер.
            //Нормально не работает при тестах на виртуалке.
            /*
            try
            {
            //Если с объектом что то не то-думаю должно высыпаться. Но как проверить пока не ясно.
            // string ttl = Browser.Title; Титл выбивает тайм аут 60сек.
            string ttl = Browser.Url;

            if (ttl==null)
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

        */

            //Измеряю затраченное время на открытие страницы.
            Stopwatch sw = new Stopwatch();
            sw.Start();
            takeScreen = 0;
            Task t = new Task(() => stopLoadPage());
            t.Start();

            try
            {
                //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
                Browser.Navigate().GoToUrl(url);
            }
            catch (Exception ex)
            {
                string str = "Exception to GoToUrl: " + ex.Message;
                saveBrowserErrorDg((int)enumBrowserError.GoUrl, str, url, filename);
                //Обработали исключение, сделали скрин шот, отправили пользователю.
            }

            //Замеряю истекшее время.
            sw.Stop();
            double elipsed = sw.Elapsed.TotalSeconds;
            elipsedTime = (float)Math.Round(elipsed, 2);
            /* 
             * Если потребуется обработка ошибок.
                  string bodyText =Browser.FindElement(By.TagName("body")).Text;
                 //Обработка ошибки 404.
                     if(bodyText.Contains("404"))return "Error 404 in body:" + bodyText; 
           */
            takeScreen = 1;
            Screenshot screenshot = null;
            try
            {
                //Создание скриншотта.
                screenshot = ((ITakesScreenshot)Browser).GetScreenshot();
            }
            catch (Exception ex1)
            {
                string str = "Exception to GetScreenshot: " + ex1.Message;
                saveBrowserErrorDg((int)enumBrowserError.GetScreenshotError, str, url, filename);
                //Копирует файл с сообщением об ошибке, если проблеммы  копирования возвращает строку с ошибкой.
                // String standartErrorImg = "noLoadPageErr.jpg";
                // return copyFile(standartErrorImg, filename); ;
            }



            //driver.findElement(By.xpath("//a[@class='button allow']/span[text()='Allow cookies']")).click();

            try
            {
                //Обрезка.
                using (var stream = new MemoryStream())
                {
                    if (screenshot == null)
                    {
                        saveBrowserErrorDg((int)enumBrowserError.ProblemWithBrowser, "screenshot==null", url, filename);
                    }

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


                    string filePathFull = Path.Combine(curentDirectory, filePath);
                    image.Save(filePathFull, new JpegEncoder() { Quality = 85 });



                }
            }
            catch (Exception ex)
            {
                //Добавить стандартную картинку.
                String str = "Exception in metod takeScreenShot where save screenshot: " + ex.Message;
                saveBrowserErrorDg((int)enumBrowserError.ProblemWithBrowser, str, url, filename);
                return "Error 702";
            }

            return null;

        }

        /// <summary>
        /// Копирует файл.
        /// </summary>
        private string copyFile(string src, string dst)
        {
            try
            {
                string filePath1 = Path.Combine("wwwroot/" + tmpDir, src);
                string filePath2 = Path.Combine("wwwroot/" + tmpDir, dst);
                File.Copy(filePath1, filePath2);
            }
            catch (Exception ex)
            {
                return "Error сopy file " + src + " to " + dst + ". " + ex.Message;
            }

            return null;
        }

        /// <summary>
        /// Проверяет содержит ли картинка только черные или белые пиксели.
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
