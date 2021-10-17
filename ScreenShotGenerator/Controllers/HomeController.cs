using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
        public IActionResult Index(string []url,string allowedReferer)
        {            

            // System.Diagnostics.Debug.WriteLine("fdssssssssssss");
            // System.Diagnostics.Debug.WriteLine("p="+url[0]);
            //?url[0]=https://google.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1

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

                List <mJobPool> ret=_screenShoter.runJob(url,strIP);
                return Json(ret);//Ok(JSON(ret));
            }
                     
          
        }

        public IActionResult Privacy()
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
