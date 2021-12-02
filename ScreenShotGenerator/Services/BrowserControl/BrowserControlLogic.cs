using ImageMagick;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterPools;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Делегат для события по завершению создания скриншота.
    /// </summary>
    /// <returns></returns>
    public delegate void BrowserEndJobOnPage(string uuid);

    /// <summary>
    /// Делегат для события по завершению жизненного цикла, исчерпания лимита по выполнению скриншоттов.
    /// </summary>
    /// <returns></returns>
    public delegate void BrowserEndLife(int BrowserId);

    /// <summary>
    /// Делегат события смерти браузера(например система убила процесс).
    /// </summary>
    public delegate void browserDie(int browserId);

    /// <summary>
    /// Логика управления браузером.
    /// </summary>
    public class BrowserControlLogic
    {
        /// <summary>
        /// Объект для управления браузером(драйвер).
        /// </summary>
        private IBrowserControl Browser;

        //Пул задач.
        private PoolTasks poolTasks;

        /// <summary>
        /// Разрешен запуск потока. Флаг используется для остановки потока.
        /// </summary>
        private bool threadIsRun;

        //Директория для хранения картинок.
        private string tmpDir;

        /// <summary>
        /// Задача выборки данных из пула и их обработки.
        /// </summary>
        private Task workTask;

        /// <summary>
        /// Делегат для сохранения сведений об ошибках браузера.
        /// </summary>
        private saveBrowserError saveBrowserErrorDg;

        /// <summary>
        /// Количество задач из пула которые браузер обрабатывает за раз.
        /// </summary>
        public int tasksPerThread { get; set; }

        /// <summary>
        /// Идентификатор браузера.
        /// </summary>
        public int browserId { get; set; }

        /// <summary>
        /// Количество сделанных скрин шоттов.
        /// </summary>
        private int countScreenShots;

        /// <summary>
        /// Список кириллических символов, для ускорения проверки url.
        /// </summary>
        private char[] cyrillicChars;

        /// <summary>
        /// Событие по завершению выполнения задачи.
        /// </summary>
        public event BrowserEndJobOnPage finishedJob;

        /// <summary>
        /// Объект ожидания события появления новой задачи.
        /// </summary>
        private AutoResetEvent waiter = new AutoResetEvent(false);

        /// <summary>
        /// Перезагружать браузер после определенного количество скриншотов. 0-не перезагружать.
        /// </summary>
        public int browserRestartAfterScreens;

        /// <summary>
        /// Флаг отправки события завершения жизненного цикла. Событие отправляется единоразово.
        /// </summary>
        private bool callEndLifeTime;

        /// <summary>
        /// Задача или задачи из пула которые обрабатывает браузер.
        /// </summary>
        List<mJobPool> tasksFromPool;

        /// <summary>
        ///Событие по завершению жизненного цикла, исчерпания лимита по выполнению скриншотов.
        /// </summary>
        /// <returns></returns>
        public event BrowserEndLife endLife;

        /// <summary>
        /// Событие потери связи с браузером(он сам вылетел или система ему помогла).
        /// </summary>
        public event browserDie eventBrowserDie;
  
        /// <summary>
        /// Запущен процесс завершения работы браузера.
        /// </summary>
        public bool beginShutdown;

        /// <summary>
        /// Событие возникающее когда браузер закрыт.
        /// </summary>
        public event browserCloseDg eventClosed;


        public BrowserControlLogic(IBrowserControl Browser_, saveBrowserError saveBrowserErrorDg_, string tmpDir)
        {
            saveBrowserErrorDg = saveBrowserErrorDg_;
            this.tmpDir = tmpDir;
            Browser = Browser_;
            Browser.saveBrowserErrorDg = saveBrowserErrorDg;
            cyrillicChars = getCyrillicChars();//Список кириллических символов, для ускорения проверки url.
            Browser.eventClosed += OnBrowserClose;
        }

        /// <summary>
        /// Обработка задач в потоке задач. Запускает отдельную задачу для проверки и обработки пула.
        /// </summary>
        /// <param name="poolTasks"></param>
        public void processPool(ref PoolTasks pool)
        {
            this.poolTasks = pool;
            threadIsRun = true; //Задача может работать.
                                //Запускаю задачу.
            workTask = new Task(processPoolThread);
            workTask.Start();
        }


        /// <summary>
        /// Запуск браузера.
        /// </summary>
        public bool startBrowser()
        {
            return Browser.runBrowser();
        }


        /// <summary>
        ///Отстанавливает браузер,завершает задачу.
        /// </summary>
        public void stopProcess()
        {
            threadIsRun = false; //Остановка процесса обработки задач, если запущен.
            waiter.Set(); //Что бы вечно не ждала система завершения задачи.
            finishedJob("");//Что бы вечно не ждала система завершения задачи.
            stopBrowser();
            //Ждем завершения задачи.
            Task.WaitAny(workTask);
        }

        /// <summary>
        /// Остановка браузера, если не остановлен.
        /// </summary>
        private void stopBrowser()
        {
            //Может случиться что браузер останавливаться или остановлен,
            //в случае критической остановки(резкое увеличение swap). Поэтому игнорируем исключения.
            Log.Information("Call stopBrowser().");
            Task.Run(()=> {
                try
                {
                    Browser.quit();
                }
                catch(Exception ex)
                {
                    Log.Information("Exception in stopBrowser()"+ex.Message);
                }
            });

           
        }

        /// <summary>
        /// Критическая остановка в случае резкого переполнения swap.
        /// </summary>
        public void CriticalStop()
        {
            beginShutdown = true;
            threadIsRun = false;
            stopBrowser();       
            waiter.Set(); //Будем логику обработки новой задачи. 
        }


        /// <summary>
        /// Обработчик события появления новой работы в ScreenShoter.
        /// </summary>
        public void OnNewJob()
        {
            waiter.Set(); //Будем логику обработки новой задачи.
        }


        /// <summary>
        /// Запуск процесса остановки браузера.
        /// </summary>
        public void shutdown()
        {
            if(beginShutdown==true) //Уже запущен процесс закрытия.
            {
                Log.Information("Try new shutdown().");
                stopBrowser();
            }

            beginShutdown = true;
            waiter.Set(); //Будем логику обработки новой задачи.
            Log.Information("shutdown()");
        }

        /// <summary>
        /// Обработчик события "браузер закрыт".
        /// </summary>
        private void OnBrowserClose(int id)
        {
            eventClosed(browserId);
        }


        /// <summary>
        /// Проверяет есть ли в пуле новые задачи, выполняет их.
        /// </summary>
        private void processPoolThread()
        {
            while (threadIsRun)
            {
                waiter.WaitOne();//Жду появления новой задачи.

                //Остановка работы браузера.
                if (beginShutdown)
                {
                    stopBrowser();
                    return;
                }

                //Выбирает из пула задач первые новые, в количестве tasksPerThread.
                //Проставляет им статус "Заблокировано браузером".
                List<mJobPool> data = poolTasks.getAndLockNewTasks(tasksPerThread, browserId);
                
                //Нет данных для обработки.
                if (data.Count == 0)
                {
                    //Сервис останавливают. Выходим.
                    if (!threadIsRun) return;

                    continue;
                }

                //Проверка лимита на выполнение скриншотов,и генерация событий.
                checkLifeTime();

                tasksFromPool = data; //Для возможности критической остановки.

                foreach (mJobPool p in data)
                {
                    //Сервис останавливают. Выходим.
                    if (!threadIsRun) return;

                    //Проверяет валидность указанного URL. Пуст, возможно ли преобразование ДНС.
                    if (!checkValidUrl(p))
                    {
                        //Формирую событие по окончанию выполнения задачи.
                        finishedJob(p.requestId); //Передаю идентификатор http запроса.
                        continue;
                    }

                    String lastError=""; //Последнее сообщение об ошибке, если есть.
                    p.fileName = getMD5(p.url) + ".jpg"; //Формирую имя файла.
                    //Путь куда сохранять файл.
                    string filePath = Path.Combine("wwwroot/" + tmpDir, p.fileName);

                    //Cоздание скриншота.
                    int answ=Browser.takeScreenShot(p.url, filePath, p.fileName, ref p.wastedTime, p.imageSize,
                        ref p.fileSize);

                    //Если получена команда остановки браузера.
                    if (IfBeginShutdown(ref data)) return;

                    //Browser die.
                    if (answ==-1)
                    {
                        actionsIfBrowserDie(ref data,p); //Действия если браузер умер.
                        return;
                    }

                    //Пустой объект screenShot.Проблеммы с сайтом.
                    if(answ == -2)
                    {
                        //Если получена команда остановки браузера.
                        if (IfBeginShutdown(ref data)) return;

                        p.status = (int)enumTaskStatus.End;
                        p.fileName = UrlErrorImg.badImg;
                        //Формирую событие по окончанию выполнения задачи.
                        finishedJob(p.requestId); //Передаю идентификатор http запроса.
                        continue;
                    }    


                    //Сервис останавливают. Выходим. Браузер мог вообще упасть и вернуть сообщение об ошибке.
                    if (!threadIsRun) return;

                    p.timestamp = DateTime.Now;

                    bool allGood; //Нет ошибок в процессе работы.

                    //Ошибка сервиса.
                    if (answ == 0)
                    {
                        allGood = false;
                        lastError = Browser.lastError; //Получаю ошибку сервиса.
                    }                       
                    else
                    {
                        // Проверяет итоговый файл на существование, на размер,
                        // и на заполнение только белым или только черным.
                        allGood = checkResultFile(out lastError, filePath,p.url);
                    }


                    //Были ли ошибки?
                    if (allGood)
                    {
                        p.status = (int)enumTaskStatus.End; //Все хорошо.
                    }
                    else
                    {
                        p.status = (int)enumTaskStatus.Error;
                        p.fileName = "Error 700."; //В запросе всегда будет одна и та же ошибка.
                        //Сохраняю логи в БД.
                        saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, lastError, p.url, p.fileName);
                    }

                    //beginShutdown

                    //Формирую событие по окончанию выполнения задачи.
                    finishedJob(p.requestId); //Передаю идентификатор http запроса.
                    countComplatedTasks(); //Увеличивает счетчик скриншоттов.
                }


            }
        }

        /// <summary>
        /// Действия если браузер умер.
        /// </summary>
        private void actionsIfBrowserDie(ref List<mJobPool> data, mJobPool p)
        {
            //Устанавливаем выполняемым задачам статус "Новая".
            ResetStatus(ref data);
            saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, "Browser DIE!", p.url, p.fileName);
            eventBrowserDie(browserId);
            stopBrowser(); //Останавливаю то что осталось от браузера(драйвер).  
        }

        /// <summary>
        /// Если получена команда остановки браузера.
        /// </summary>
        /// <returns></returns>
        private bool IfBeginShutdown(ref List<mJobPool> data)
        {
            //Если критическая остановка.
            if (beginShutdown)
            {
                //Устанавливаем выполняемым задачам статус "Новая".
                ResetStatus(ref data);
                stopBrowser(); //Останавливаю то что осталось от браузера(драйвер).  
                return true;
            }

            return false;
        }


        /// <summary>
        /// Меняет статус не выполненным задачам на "Новая".
        /// </summary>
        private void ResetStatus(ref List<mJobPool> data)
        {
            string requestId=null;
            foreach (mJobPool t in data)
            {
                if (t.status == (int)enumTaskStatus.LockByBrowser)
                {
                    t.status = (int)enumTaskStatus.NewTask;
                    requestId = t.requestId; //Сохраняю идентификатор запроса.
                }
                    
            }

            //Если были задачи-генерирую событие что обработал.
            if(requestId!=null)
            finishedJob(requestId); //Передаю идентификатор http запроса.
        }

        /// <summary>
        /// Увеличивает счетчик скриншоттов.
        /// </summary>
        private void countComplatedTasks()
        {
            if (countScreenShots < int.MaxValue)
                countScreenShots++;
            else
                countScreenShots = 0;
        }

        /// <summary>
        /// Проверяет валидность указанного URL.
        /// </summary>
        /// <returns></returns>
        private bool checkValidUrl(mJobPool p)
        {
            //Проверка урл на пустоту.
            if (String.IsNullOrEmpty(p.url))
            {
                string errMsg = "Error:Empty url!";
                p.status = (int)enumTaskStatus.End;
                p.fileName = UrlErrorImg.badUrl;
                //Cохраняю логи в БД.
                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);
                return false;
            }

            //Запрещенный доступ к браузеру.
            if(p.url.Contains("about:"))
            {
                string errMsg = "Error:Access to the settings page!Rejected. url=" + p.url;
                p.status = (int)enumTaskStatus.End;
                p.fileName = UrlErrorImg.badUrl;
                //Cохраняю логи в БД.
                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);
                Log.Error(errMsg);
                return false;
            }

            try
            {
                Uri uri = new Uri(p.url);

                //Поиск кирилических символов в домене.
                bool res = uri.Host.Any(ch => Array.BinarySearch(cyrillicChars, ch) >= 0);

                if (res)
                {
                    //Если сайт кирилицой, перекодируем его в Punycode иначе dns не поймет.
                    System.Globalization.IdnMapping idn = new System.Globalization.IdnMapping();
                    string punyUrl = idn.GetAscii(uri.Host);
                    IPAddress[] aIp = Dns.GetHostAddresses(punyUrl);
                    return true;
                }

                IPAddress[] addresses = Dns.GetHostAddresses(uri.Host);
            }
            catch
            {
                //Проблемы с адресом.
                p.status = (int)enumTaskStatus.End;
                p.fileName = UrlErrorImg.badUrl;
                // string errMsg = "Error:dnsNotFound!"; //Не будем засорять лог.
                //saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Возвращает кириллические символы.
        /// </summary>
        /// <returns></returns>
        private static char[] getCyrillicChars()
        {
            return Enumerable
                            .Range(UnicodeRanges.Cyrillic.FirstCodePoint, UnicodeRanges.Cyrillic.Length)
                            .Select(ch => (char)ch)
                            .ToArray();
        }

        /// <summary>
        /// Проверяет итоговый файл на существование, на размер, и на заполнение только белым или только черным.
        /// Если все хорошо=true.
        /// </summary>
        /// <param name="errMess"></param>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        private static bool checkResultFile(out string errMess, string pathToFile,string url)
        {
            errMess = null;
            int countOperation = 1; //Счетчик операций, для определения на какой возникло исключение.
            try
            {
                //Почему то файл не создался. 
                if (!checkExistFile(pathToFile))
                {
                    errMess = "File no exist.";
                    return false;
                }
                countOperation++;

                //Почему то файл пуст.
                if (!checkFileSize(pathToFile))
                {
                    errMess = "File length is 0.";
                    return false;
                }
            }
            catch(Exception ex)
            {
                errMess = "Exception in checkResultFile(step="+ countOperation.ToString()+ ";url="+url
                    +"):"+ ex.Message;
                return false;
            }
                       

            /*
            //Проверяет не вернул ли браузер черную или белую картинку.
            int chkColorErr = imgOnlyBlackOrWhite(pathToFile);
            if (chkColorErr != 0)
            {
                errMess = "Image contains only " + ((chkColorErr == 1) ? "white" : "black") + " pixels.";
                return false;
            }
            */

            return true;
        }


        /// <summary>
        /// Проверяю существования файла в папке кеша.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool checkExistFile(string path)
        {
            bool exists = System.IO.File.Exists(path);
            return exists;
        }

        /// <summary>
        /// Проверяю размер файла.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool checkFileSize(string path)
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
        private static string getMD5(String input)
        {

            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                return string.Join("", result.Select(x => x.ToString("X2"))).ToLower();
            }

        }

        /// <summary>
        /// Проверка лимита на выполнение скриншотов,и генерация событий.
        /// </summary>
        private void checkLifeTime()
        {
            //Не включен режим бесконечной работы.Событие не генерировали. Превышен лимит.
            if ((browserRestartAfterScreens != 0) && (!callEndLifeTime) && (countScreenShots > browserRestartAfterScreens))
            {
                callEndLifeTime = true; //Запрет повторной генерации события.
                //Генерирую события окончания срока эксплуатации браузера.
                endLife(browserId);

            }
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
