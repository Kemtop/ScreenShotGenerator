using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Список ошибок браузера Firefox.
    /// </summary>
    public class FireFoxErrors
    {
        /// <summary>
        /// Открыто Alert окно.
        /// </summary>
        public static string  userPromtDialog = "Dismissed user prompt dialog";

      
        /// <summary>
        /// Список сообщений свидетельствующих о выходе браузера из строя.
        /// </summary>        
        private static string[] brokenMessages =
        {
            "Tried to run command without establishing a connectio",
            "timed out after 60 seconds"
        };


        /// <summary>
        /// На основании сообщения определяет не сломался ли браузер.true-сломался.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool browserBroken(string msg)
        {
            foreach (string s in brokenMessages)
            {
                if (msg.Contains(s))
                    return true;
            }
            return false;
        }




        /// <summary>
        /// Не критические ошибки при загрузке страницы.
        /// </summary>
        private static string[] noCriticalPageErrors = {
            "InsecureCertificate",
            "Reached error page: about:neterror",
            "TimedPromise"
        };
        //TimedPromise timed out after 8000 m удалить
        //"Timeout loading page", //Истек таймаут. удалить


        /// <summary>
        /// Определяет является ли данная ошибка критической. Используется при загрузке страницы.
        /// </summary>
        /// <param name="Exception"></param>
        /// <returns></returns>
        public static bool IsCriticalLoadPageError(string Exception)
        {
            foreach(string s in noCriticalPageErrors)
            {
                if (Exception.Contains(s))
                    return false;
            }
            return true;
        }
    }
}
