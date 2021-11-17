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
using ScreenShotGenerator.Services.ScreenShoterPools;

namespace ScreenShotGenerator.Controllers
{
    public class HomeController : Controller
    {
        private readonly IScreenShoter _screenShoter;//Объект создания скрин шота.
        private IHttpContextAccessor HttpContextAccessor { get; }

        public HomeController(IScreenShoter screenShoter,
            IHttpContextAccessor httpContextAccessor)
        {
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
                List<mUserJson> ret =_screenShoter.runJob(url,strIP, HttpContextAccessor.HttpContext.TraceIdentifier);
                return Json(ret);
            }
                     
          
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
