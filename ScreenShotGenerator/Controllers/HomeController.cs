using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ScreenShotGenerator.Models;
using ScreenShotGenerator.Services;
using ScreenShotGenerator.Services.ScreenShoterLogic;

namespace ScreenShotGenerator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IScreenShoter _screenShoter;//Объект создания скрин шота.
        private IHttpContextAccessor HttpContextAccessor { get; }

        public HomeController(ILogger<HomeController> logger, IScreenShoter screenShoter,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _screenShoter = screenShoter;
            HttpContextAccessor = httpContextAccessor;
        }


        /// <summary>
        /// Функция для обработки главной страницы.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult Index(string []url, string allowedReferer)
        {
            //string url1
            // System.Diagnostics.Debug.WriteLine("fdssssssssssss");
            // System.Diagnostics.Debug.WriteLine("p="+url[0]);
            //?url[0]=https://google.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1


            //bool useEnc = false;
           //if (useEncoding != null) useEnc = true;

          
            //Запрещено пользоваться посторонним лицам.
            if (allowedReferer==null)
            {              

               return View();
            }
            else
            {
              
                //Получаю ip пользователя.
                IPAddress userIp = HttpContextAccessor.HttpContext.Connection.RemoteIpAddress;
                string strIP = userIp.ToString();
                //url нормально понимают преобразованные символы в запросе типа %23 и т.д.
                List<mUserJson> ret =_screenShoter.runJob(url,strIP);
                return Json(ret);
            }
                     
          
        }

        //Для 
        // string parameters1 = HttpContextAccessor.HttpContext.Request.Scheme;// QueryString.ToString();
        //
        //Удалить если нельзя.
        private List<string> parceGetParametrs(string inStr,bool useEncoding)
        {
            List<string> urls = new List<string>();

            string[] url =inStr.Split("url[");

            foreach(string u in url)
            {
                if(u.Contains("]="))
                {
                    int pos = u.IndexOf("]=");
                    if (pos == -1) continue;
                    //Вырезаю строку после "]=" и до последнего символа(который будет&).
                    string str1 = u.Substring(pos+2,u.Length-(pos+3));

                    if(useEncoding)
                    {
                        str1 = HttpUtility.UrlDecode(str1);
                    }

                    urls.Add(str1);
                }
            }

            //Исправляю проблеммы


            return urls;

        }


        public IActionResult Privacy()
        {
            return View();
        }


        public IActionResult AccessDenied()
        {
            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
