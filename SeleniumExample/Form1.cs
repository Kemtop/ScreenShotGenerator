using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SeleniumExample
{
    public partial class Form1 : Form
    {
        IWebDriver Browser;

        
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Browser.Manage().Window.Size = new Size(1280, 1060);
            Browser.Manage().Window.Maximize(); //Разворачиваем браузер на весь экран.
            /*
            //
                                                //Переходим по определенному адресу.
                                                // Browser.Navigate().GoToUrl("http://google.com");
           
            // Browser.Navigate().GoToUrl("https://www.dns-shop.ru/");
            Browser.Navigate().GoToUrl("https://google.com");

            Screenshot ss = ((ITakesScreenshot)Browser).GetScreenshot();
            ss.SaveAsFile("screen.jpg");
            */
            takeScreenShot("https://www.dns-shop.ru/");
            takeScreenShot("https://google.com");
            takeScreenShot("https://ya.ru");
            /*
            Thread.Sleep(2000);
            takeScreenShot("https://www.dns-shop.ru/");
            takeScreenShot("https://google.com");
            takeScreenShot("https://ya.ru");*/
        }


        private void takeScreenShot(string url)
        {
            Browser.Navigate().GoToUrl(url);
            /*
            bool wait = new WebDriverWait(Browser, TimeSpan.FromSeconds(60)).Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

            if (wait == true)
            {
                //Your code
            }
            */
            Thread.Sleep(2000);
            Screenshot ss = ((ITakesScreenshot)Browser).GetScreenshot();
            ss.SaveAsFile("screen"+DateTime.Now.ToString("hh_mm_ss_fff")+".jpg");
        }

        private void buttonRunInBG_Click(object sender, EventArgs e)
        {
            try
            {
                /*
               var chromeOptions = new ChromeOptions();
               chromeOptions.AddArgument("--headless");
               chromeOptions.AddArguments("window-size=1280,1060");
                Browser = new OpenQA.Selenium.Chrome.ChromeDriver(chromeOptions);
                */
                 Browser = new OpenQA.Selenium.Chrome.ChromeDriver();


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                //Исключение если не верная версия браузера.
            }


         
        }

        private void button2_Click(object sender, EventArgs e)
        {

            Browser.Quit();
        }
    }
}
