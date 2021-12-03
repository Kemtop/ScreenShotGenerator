using ImageMagick;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterPools;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// Путь к текущей папке.
        /// </summary>
        private string curentDirectory;

        /// <summary>
        /// Были ли призрачные ошибки?
        /// Вроде Alert окон, открытия новых вкладок и
        /// Exception in reopenWindow: Dismissed user promp t dialog: Player is not supporte
        /// </summary>
        private bool hasGhostError;

        /// <summary>
        /// Делегат для сохранения сведений об ошибках браузера.
        /// </summary>
        public saveBrowserError saveBrowserErrorDg { get; set; }

        /// <summary>
        /// Пустая страница на которую заходит браузер.
        /// </summary>
        public string blankPage { get; set; }

        /// <summary>
        /// Последний url на который ходил браузер.
        /// </summary>
        private string lastUrl;

        /// <summary>
        /// Объект для выполнения js скриптов.
        /// </summary>
        IJavaScriptExecutor JsExecuter;

        /// <summary>
        /// Возвращает ошибку.
        /// </summary>
        public string lastError { get; private set; }

        /// <summary>
        /// Событие возникающее когда браузер закрыт.
        /// </summary>
        public event browserCloseDg eventClosed;


        public ImpBrowserControlFireFox(int pageLoadTimeouts, int javaScriptTimeouts)
        {
            //Путь к рабочей директории приложения.
            curentDirectory = Directory.GetCurrentDirectory();
            this.pageLoadTimeouts = pageLoadTimeouts;
            this.javaScriptTimeouts = javaScriptTimeouts;

            //Тест
            
            bool exists = System.IO.File.Exists(getAlertPathFile());
            if (!exists)
            {
                using (StreamWriter writer = System.IO.File.CreateText(getAlertPathFile()))
                {
                    writer.WriteLine("------------------");
                }
            }
        }
        

        string getAlertPathFile()
        {
            return curentDirectory + @"/alerts.txt";
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
                JsExecuter = (IJavaScriptExecutor)Browser;

            }
            catch (Exception ex)
            {
                //Исключение если не верная версия браузера.
                String msg = "Exeption on metod runBrowser(user=" + Environment.UserName + "):" + ex.Message;
                Log.Error(msg);
                return false;
            }
                        
            Log.Information("Run FireFox. Control Module Version 1.14.");
            return true;
        }
 

     
        /// <summary>
        /// Создает скрин шот, в случае критических ошибок возвращает false.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public int takeScreenShot(string url, string filePath, string filename, ref float elipsedTime,
            ImageSize imgSize, ref UInt32 outSize)
        {
            bool hasException = false; //Были ли исключения?

            //Переход на пустую страницу для исключения ситуации когда новый сайт по особенному долго
            //грузиться и в итоге получается скрин старого сайта.
            //Анализируем ошибки и делаем вывод можно ли дальше работать.
            if (!Navigate(blankPage, ref hasException, filename)) return -1; //Браузер не работает.
            //По непонятным причинам браузер не хочет обрабатывать страницу(строку с html),выбивает таймаут. 
            if(hasException) 
            {
                SaveBrowserError("Warning:" +
                    "Time out on blank page after(last url="+ lastUrl 
                    + ",curent url:"+url+") :", lastError,url, filename); //Сохраняю в лог.
            }

            //Измеряю затраченное время на открытие страницы.
            Stopwatch sw = new Stopwatch();
            sw.Start();

            //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
            if (!Navigate(url, ref hasException, filename)) return -1; //Браузер не работает.                                                                       
            StopLoadScripts(url);//Останавливает выполнение js скриптов на странице.


            //Замеряю истекшее время.
            sw.Stop();
            double elipsed = sw.Elapsed.TotalSeconds;
            elipsedTime = (float)Math.Round(elipsed, 2);

            //Делаю скриншот.
            string ExceptionMessage = ""; //Сообщение исключения. 
            bool beginTry = true; //Пытаюсь сделать скрин.
            Screenshot screenshot = null;
            //Если переживаешь насчет вечного цикла-Цикл обязательно остановиться,браузер остановиться по завершению службы,
            //в итоге получим потерю связи,обработку Die сообщения и выйдем из цикла.
            int retryCnt = 0;
            while (beginTry)
            {
                screenshot = takeScreen(ref hasException, ref ExceptionMessage);
                //Возникло исключение.
                if (hasException)
                {
                    //Появилось alert окно.
                    if (ExceptionMessage.Contains(FireFoxErrors.userPromtDialog))
                    {
                        
                        using (StreamWriter w = File.AppendText(getAlertPathFile()))
                        {
                            w.WriteLine(url);
                        }
                        //return -2;

                        
                        if (retryCnt > 10)//Слишком много попыток.
                        {
                            SaveBrowserError("Too many try close alerts for url=" + url, " ", url," ");
                            return -2;
                        }
                                              
                        aceeptAlert(url);
                        Thread.Sleep(800);//Жду может что то под грузится.
                        retryCnt++;
                        continue;
                    }

                    //Браузер умер.
                    if(FireFoxErrors.browserBroken(ExceptionMessage))
                    {
                        SaveBrowserError("Exception to GetScreenshot: Browser Die.", ExceptionMessage, url, filename);
                        return -1;
                    }
                                      
                    //Не ведомая нам ошибка.
                    SaveBrowserError("Exception to GetScreenshot:", ExceptionMessage, url, filename);
                    break;

                }
                else //Скрин сделали без исключений.
                    break;
            }    

            //Если какие то проблемы создания скрина.
            if(screenshot==null)
            {
                SaveBrowserError("Null in screenshot"," ", url," ");
                return -2; //Не смог сделать скрин шот по не известным причинам.
            }

         
            try
            {  
                //Конвертирую и сохраняю изображение в файл.
                string filePathFull = Path.Combine(curentDirectory, filePath);
                ThingsForBrowser.reduceImage(screenshot.AsByteArray, imgSize, filePathFull,ref outSize);

                //Проверка наличия открытых нескольких окон.И их закрытие. Если этого не делать,страницы складываются
                //в swap, что приводит к его переполнению.
                //checkManyOpenWindows(url);
            }
            catch (Exception ex)
            {
                //Добавить стандартную картинку.
                lastError = "Exception in metod takeScreenShot where save screenshot: " + ex.Message;
                SaveBrowserError(lastError, "", url, filename);
                return 0;
            }
            //Последний URL на который ходил браузер.
            lastUrl = url;
            return 1;
        }

        /// <summary>
        /// Останавливает выполнение js скриптов на странице.
        /// </summary>
        private void StopLoadScripts(string url)
        {
            try
            {
                JsExecuter.ExecuteScript("window.stop();"); //Останавливаю загрузку скриптов.
            }
            catch(Exception ex)
            {
                Log.Information("Exception in StopLoadScripts(url="+url+"): "+ex.Message);
            }
        }


        /// <summary>
        /// Переходит по ссылке, в случае не критических проблем возвращает true.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="ExceptionMessage"></param>
        /// <returns></returns>
        private bool Navigate(string url,ref bool hasException, string filename)
        {
            hasException = false;
            try
            {
                //Загружаем страницу, метод синхронный и пока страница не загрузиться дальше не идет.
                Browser.Navigate().GoToUrl(url);
            }
            catch (OpenQA.Selenium.WebDriverTimeoutException te)
            {
                //Похоже истек таймаут.
                lastError = te.Message;//Последняя ошибка.
                hasException = true;
            }
            catch (Exception ex)
            {
                string ExceptionMessage = ex.Message;
                lastError = ExceptionMessage; //Последняя ошибка.
                hasException = true;
   
                //Браузер умер.
                if (FireFoxErrors.browserBroken(ExceptionMessage))
                {
                    SaveBrowserError("Exception to GetScreenshot: Browser Die.", ExceptionMessage, url, filename);
                    return false;
                }

                if (FireFoxErrors.IsCriticalLoadPageError(ExceptionMessage)) //Критическое исключение?.
                {
                    SaveBrowserError("Exception to GoToUrl:", ExceptionMessage, url, filename); //Сохраняю в лог.
                }
                              
            }

            return true;
        }

        /// <summary>
        /// Закрывает всплывающее окно.
        /// </summary>
        private bool aceeptAlert(string url)
        {
            hasGhostError = true; //Возможно проблемный сайт.

            try
            {
                Browser.SwitchTo().Alert().Dismiss();//  Accept(); //Закрываю алерт окно, и повторно пытаюсь сделать скрин.                                               
                StopLoadScripts(url);//Останавливает выполнение js скриптов на странице.
            }
           catch(Exception ex)
            {
                string ExceptionMessage = ex.Message;
                //Если Alert().Accept()  срабатывает,то возникает пустое исключение.
                //Не удалось найти информацию по этому поводу почему так.
                if (String.IsNullOrEmpty(ExceptionMessage)) return true;

                //Не известное исключение.
                SaveBrowserError("Exception in aceeptAlert:", ExceptionMessage,url, " ");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Закрывает старую вкладку, открывает новую. Возвращаю false если браузер перестал отвечать.
        /// </summary>
        /// <returns></returns>
        private bool reopenWindow()
        {
            if (!hasGhostError) return true; //Не было проблемных сайтов.

            try
            {               
                hasGhostError = false; //Пришли чистить проблемы.
                //Это тоже не помогает ерунду закрывать.
                aceeptAlert("reopenWindow");

                Browser.SwitchTo().NewWindow(WindowType.Tab);
                List<string> brWindow = Browser.WindowHandles.ToList();
                
                Browser.SwitchTo().Window(brWindow[0]);
                Browser.Close();
                Browser.SwitchTo().Window(brWindow[1]);
            }
            catch (Exception ex)
            {
                if (FireFoxErrors.browserBroken(ex.Message)) return false;
                SaveBrowserError("Exception in reopenWindow:", ex.Message, " ", " ");
            }

            return true;
        }

        /// <summary>
        /// Делаю снимок экрана, в случае исключения сообщаю об ошибке.
        /// </summary>
        /// <param name="hasException"></param>
        /// <param name="ExceptionMessage"></param>
        /// <returns></returns>
        private Screenshot takeScreen(ref bool hasException,ref string ExceptionMessage)
        {
            hasException = false;
            Screenshot screenshot = null;
            try
            {
                //Создание скриншотта.
                screenshot = ((ITakesScreenshot)Browser).GetScreenshot(); 
            }
            catch (Exception ex)
            {               
                hasException = true;
                ExceptionMessage = ex.Message;
            }

            return screenshot;
        }


        /// <summary>
        /// Сохраняет в БД ошибки браузера.
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Exception"></param>
        private void SaveBrowserError(string Title,string Exception,string url, string filename)
        {
           saveBrowserErrorDg((int)enumBrowserError.GetScreenshotError, Title+" "+Exception, url, filename);
        }

        public void quit()
        {
            Browser.Quit();
            Log.Information("Browser quit.");
            eventClosed(0); //Генерирую событие что браузер закрыт. Ид не знаем=0.
        }

        /// <summary>
        /// Проверка наличия открытых нескольких окон. И их закрытие.
        /// </summary>
        private void checkManyOpenWindows(string url)
        {
            //Проверка наличия нескольких открытых окон.
            List<string> brWindow = Browser.WindowHandles.ToList();
            if (brWindow.Count > 1)
            {
                hasGhostError = true; //Возможно проблемный сайт.

                Log.Information("----------Warning:Browser open " + brWindow.Count.ToString() 
                    + " window. Begin close.------");
                Log.Information("URL="+ url);

                foreach (var b in brWindow)
                    Log.Information("Window id=" + b);

                Log.Information("---------------");

                //Закрыть все вкладки, оставить одну.
                int tabs = brWindow.Count;
                //Закрываю все вкладки с конца, кроме первой.
                while (tabs>1)
                {                 
                    Browser.SwitchTo().Window(brWindow[tabs - 1]); //Переключаюсь на последнюю открытую.
                    try
                    {  
                        //Можем получить проблеммы при определения заголовков и урл, обвернем в трай катч.
                        Log.Information("Closing id=" + brWindow[tabs - 1] + ";Title=" + Browser.Title +
                        ";url=" + Browser.Url);
                    }
                    catch (Exception) {; }
                    
                    Browser.Close(); //Закрываю текущую.
                   
                    tabs--;
                }

                Browser.SwitchTo().Window(brWindow[0]); //Переключаюсь на первую.
            }
        }

        private void InstallAddons()
        {
            //Путь к рабочей директории приложения.
            string path = curentDirectory + @"\firefoxAddons.txt";
            bool exists = System.IO.File.Exists(path);
            if(!exists)
            {
                //Попытка установить расширение.
                if (!InstallAddon("https://addons.mozilla.org/en-US/firefox/addon/adblock-plus/")) return; 

                using (StreamWriter writer = System.IO.File.CreateText(path))
                {
                    writer.WriteLine("Install AddBlock");
                }
            }
        }


        bool IsAlertShown(IWebDriver driver)
        {
            try
            {
                Thread.Sleep(300);
                driver.SwitchTo().Alert();
            }
            catch (NoAlertPresentException e)
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// Удалить.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool InstallAddon(string url)
        {
            try
            {
                Log.Information("Install Addons.");
                Browser.Navigate().GoToUrl(url);
                Log.Information("Browser in " + url);


                string currentHandle = Browser.CurrentWindowHandle;
                ReadOnlyCollection<string> originalHandles = Browser.WindowHandles;

                //Вызываю всплывающее окно.
                Browser.FindElement(By.XPath(@"//a[text()='Add to Firefox']")).Click();

                // WebDriverWait.Until <T> ждет, пока делегат не вернется
                // ненулевое значение для типов объектов. Мы можем использовать это
                // поведение для возврата дескриптора всплывающего окна.
                WebDriverWait wait = new WebDriverWait(Browser, TimeSpan.FromSeconds(60));
                string popupWindowHandle = wait.Until<string>((d) =>
                {
                    string foundHandle = null;

                    // Вычтите список известных дескрипторов. В случае одиночного
                    // всплывающее окно, список newHandles будет иметь только одно значение.
                    List<string> newHandles = Browser.WindowHandles.Except(originalHandles).ToList();
                    if (newHandles.Count > 0)
                    {
                        foundHandle = newHandles[0];
                    }

                    return foundHandle;
                });

                Browser.SwitchTo().Window(popupWindowHandle);
                Browser.FindElement(By.XPath(@"//a[text()='Добавить']")).Click();

                // Do whatever you need to on the popup browser, then...
                Browser.Close();
                Browser.SwitchTo().Window(currentHandle);


                /*
                 * 
                 * 
                 * 
                //@"//button[text()='Add to Firefox']
                Browser.FindElement(By.XPath(@"//a[text()='Add to Firefox']")).Click();
                // find_element_by_xpath('//span[text()="OK"]').click()
                //Ждем всплывающее окно.
                Log.Information("Wait alert window.." + url);

              


                WebDriverWait wait = new WebDriverWait(Browser, TimeSpan.FromSeconds(60));
                wait.Until(Browser => IsAlertShown(Browser));
                IAlert alert = Browser.SwitchTo().Alert();
                alert.Accept();
                */
                Log.Information("Install Addons complated!");

            }
            catch(Exception ex)
            {
                SaveBrowserError("Can't install Addons", ex.Message, url, " ");
                Log.Error(ex.Message);
                return false;
            }

            return true; 
        }



    }
}

