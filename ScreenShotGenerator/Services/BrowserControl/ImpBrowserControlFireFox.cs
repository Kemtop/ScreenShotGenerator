using ImageMagick;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
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
    /// Реализация управления браузером FireFox.
    /// </summary>
    public class ImpBrowserControlFireFox : IBrowserControl
    {
        /// <summary>
        /// Объект для управления браузером(драйвер).
        /// </summary>
        private IWebDriver Browser;

        /// <summary>
        /// Тайм аут загрузки страницы.
        /// </summary>
        private int pageLoadTimeouts { get; set; }
        /// <summary>
        /// Тайм аут загрузки скрипта.
        /// </summary>
        private int javaScriptTimeouts { get; set; }


        /// <summary>
        /// Делегат для сохранения сведений об ошибках браузера.
        /// </summary>
        public saveBrowserError saveBrowserErrorDg { get; set; }

        /// <summary>
        /// Путь к текущей папке.
        /// </summary>
        private string curentDirectory;

        /// <summary>
        /// Пустая страница на которую заходит браузер.
        /// </summary>
        public string blankPage { get; set; }

        public ImpBrowserControlFireFox(int pageLoadTimeouts, int javaScriptTimeouts)
        {
            //Путь к рабочей директории приложения.
            curentDirectory = Directory.GetCurrentDirectory();
            this.pageLoadTimeouts = pageLoadTimeouts;
            this.javaScriptTimeouts = javaScriptTimeouts;
        }

   


        /// <summary>
        /// Считываю из appsettings.json опции браузера.
        /// </summary>
        /// <returns></returns>
        private FirefoxOptions createOptions()
        {
            //Читаю настройки браузера.
            Dictionary<string, object> Dic = ThingsForBrowser.readConfigBrowser("Firefox");

            FirefoxOptions options = new FirefoxOptions();
            //Должна быть установлена версия дравера 0.30.0.1 иначе работать не будет.
            bool boolValue;
            int intValue;
            float floatValue;

            foreach (KeyValuePair<string, object> l in Dic)
            {
                //Увы но не смотря на типы в json все значения приходят с типом string.
                if (Boolean.TryParse(l.Value.ToString(), out boolValue))
                {
                    options.SetPreference(l.Key, boolValue);
                    continue;
                }

                if (Int32.TryParse(l.Value.ToString(), out intValue))
                {
                    options.SetPreference(l.Key, intValue);
                    continue;
                }

                if (float.TryParse(l.Value.ToString(), out floatValue))
                {
                    options.SetPreference(l.Key, floatValue);
                    continue;
                }


                //И ни то и не другое,значит точно строка.
                options.SetPreference(l.Key, l.Value.ToString());

            }

            return options;
        }



        /// <summary>
        /// Настраивает драйвер, и вызвает запуск браузера.
        /// </summary>
        public bool runBrowser()
        {
            try
            {
                // Считываю из appsettings.json опции браузера.
                FirefoxOptions options = createOptions();

                options.AddArgument("start-maximized");
                options.AddArgument("disable-infobars");
                options.AddArgument("--disable-extensions");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-application-cache");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--disable-dev-shm-usage");



                //Отключить загрузку файлов.
                //Путь к исполняемому файлу драйвера должен быть установлен системным свойством
                Browser = new FirefoxDriver(options);


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

            Log.Information("Run FireFox.");
            return true;
        }
 

     
        /// <summary>
        /// Создает скрин шот, в случае ошибок возвращает строку.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public  string takeScreenShot(string url, string filePath, string filename, ref float elipsedTime,
            ImageSize imgSize, ref UInt32 outSize)
        {
            //Переход на пустую страницу для исключения ситуации когда новый сайт по особенному долго
            //грузиться и в итоге получается скрин старого сайта.
            try
            {
                //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
                Browser.Navigate().GoToUrl(blankPage);
            }
            catch (Exception ex)
            {
                string str = "Exception to go blankPage.html: " + ex.Message;
                saveBrowserErrorDg((int)enumBrowserError.GoUrl, str, url, filename);
                //Обработали исключение, сделали скрин шот, отправили пользователю.
                if (ex.Message.Contains("Can't open blank page."))
                {
                    return "Error 704.Can't open blank page.";
                }

            }


            //Измеряю затраченное время на открытие страницы.
            Stopwatch sw = new Stopwatch();
            sw.Start();

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
                if(ex.Message.Contains("is not a valid URL"))
                {
                    return "Error 703";
                }

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


             //Если все таки получиться отключить куки, тогда прийдеться использовать это.
            //driver.findElement(By.xpath("//a[@class='button allow']/span[text()='Allow cookies']")).click();

            try
            {

                string filePathFull = Path.Combine(curentDirectory, filePath);
                ThingsForBrowser.reduceImage(screenshot.AsByteArray, imgSize, filePathFull,ref outSize);
                //screenshot.SaveAsFile(filePathFull, ScreenshotImageFormat.Jpeg);
                //screenshot = null;

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

        public void quit()
        {
            Browser.Quit();
        }

    }
}

