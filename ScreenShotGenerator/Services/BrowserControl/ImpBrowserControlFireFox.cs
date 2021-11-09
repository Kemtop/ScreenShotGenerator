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
    /// Реализация управления браузером Chrome.
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


        public ImpBrowserControlFireFox(int pageLoadTimeouts, int javaScriptTimeouts)
        {
            //Путь к рабочей директории приложения.
            curentDirectory = Directory.GetCurrentDirectory();
            this.pageLoadTimeouts = pageLoadTimeouts;
            this.javaScriptTimeouts = javaScriptTimeouts;
        }


        /// <summary>
        /// Читает конфигрурацию браузера.
        /// </summary>
        /// <param name="browserName"></param>
        /// <returns></returns>
        private Dictionary<string, object> readConfigBrowser(string browserName)
        {
            //Получаю конфигурацию.
            IConfigurationRoot config_ = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                 .Build();

            List<IConfigurationSection> lines = config_.GetSection(browserName)
                    .GetChildren().ToList();

            Dictionary<string, object> Dic = new Dictionary<string, object>();
            foreach (IConfigurationSection s in lines)
            {
                Dic[s.Key] = s.Value;
            }

            return Dic;
        }

        /// <summary>
        /// Считываю из appsettings.json опции браузера.
        /// </summary>
        /// <returns></returns>
        private FirefoxOptions createOptions()
        {
            //Читаю настройки браузера.
            Dictionary<string, object> Dic = readConfigBrowser("Firefox");

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
            //Log.Information("stopLoadPage");
        }


        /// <summary>
        /// Создает скрин шот, в случае ошибок возвращает строку.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public string takeScreenShot(string url, string filePath, string filename, ref float elipsedTime)
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

        public void quit()
        {
            Browser.Quit();
        }

    }
}

