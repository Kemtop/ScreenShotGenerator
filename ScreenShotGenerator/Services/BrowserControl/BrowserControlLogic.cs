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
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
    /// Логика управления браузером.
    /// </summary>
    public class BrowserControlLogic
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
        /// Событие по завершению выполнения задачи.
        /// </summary>
        public event BrowserEndJobOnPage finishedJob;

        /// <summary>
        /// Объект ожидания события появления новой задачи.
        /// </summary>
        private AutoResetEvent waiter = new AutoResetEvent(false);

        public BrowserControlLogic(IBrowserControl Browser_, saveBrowserError saveBrowserErrorDg_, string tmpDir)
        {
            saveBrowserErrorDg = saveBrowserErrorDg_;
            this.tmpDir = tmpDir;
            Browser = Browser_;
            Browser.saveBrowserErrorDg = saveBrowserErrorDg;
           
        }

        /// <summary>
        /// Обработка задач в потоке задач. Запускает отдельную задачу для проверки и обработки пула.
        /// </summary>
        /// <param name="poolTasks"></param>
        public void processPool(ref poolTasks pool, ref object locker)
        {
            this.poolTasks = pool;
            this.lockPoolTasks = locker;
                    

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
            Browser.quit();
            //Ждем завершения задачи.
            Task.WaitAny(workTask);
        }

        /// <summary>
        /// Обработчик события появления новой работы в ScreenShoter.
        /// </summary>
        public void OnNewJob()
        {
            waiter.Set(); //Будем логику обработки новой задачи.
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

                waiter.WaitOne();//Жду появления новой задачи.


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
                  
                    continue;
                }

                //Превышен лимит.
                if(countScreenShots>10000)
                {
                    //Перезапуск браузера.
                    Browser.quit();
                    countScreenShots = 0;
                    Browser.runBrowser();
                }


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

                    String lastError = null; //Последнее сообщение об ошибке, если есть.
                    p.fileName = getMD5(p.url) + ".jpg"; //Формирую имя файла.
                    //Путь куда сохранять файл.
                    string filePath = Path.Combine("wwwroot/" + tmpDir, p.fileName);


                    //Cоздание скриншота.
                    // Log.Information("take "+p.url+";Browser="+browserId.ToString());                  
                    string err = Browser.takeScreenShot(p.url, filePath, p.fileName, ref p.wastedTime,p.imageSize,
                        ref p.fileSize);
                    //Log.Information("size="+outSize.ToString());

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
                            {
                                p.status = (int)enumTaskStatus.End; //Все хорошо.
                            }                                
                            else
                            {
                                p.status = (int)enumTaskStatus.Error;
                                p.fileName = lastError;
                                //Сохраняю логи в БД.
                                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, lastError, p.url, p.fileName);
                            }

                            //Формирую событие по окончанию выполнения задачи.
                            finishedJob(p.requestId); //Передаю идентификатор http запроса.

                            break;
                        }
                                                
                    }

                        countScreenShots++;

                }

               
            }
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
                //Сохраняю логи в БД.
                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);
                return false;
            }


            try
            {
                //string u = Uri.UnescapeDataString(p.url);
                Uri uri = new Uri(p.url);
                string t= uri.DnsSafeHost;
                IPAddress[] addresses = Dns.GetHostAddresses(uri.Host);
            }
            catch
            {
                //Проблемы с адресом.
                string errMsg = "Error:dnsNotFound!";
                p.status = (int)enumTaskStatus.End;
                p.fileName = UrlErrorImg.badUrl;
                //Сохраняю логи в БД.
                saveBrowserErrorDg((int)enumBrowserError.PostProcessingCheckError, errMsg, p.url, p.fileName);
                return false;
            }

            return true;
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
