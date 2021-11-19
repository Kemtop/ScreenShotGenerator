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
        /// Потеря связи с браузером. Очевидно его убила система.
        /// </summary>
        public static string lossConnection = "Tried to run command without establishing a connectio";

        /// <summary>
        /// Не критические ошибки при загрузке страницы.
        /// </summary>
        private static string[] noCriticalPageErrors = {
            "Timeout loading page", //Истек таймаут.
            "InsecureCertificate",
            "Reached error page: about:neterror"
        };
        //TimedPromise timed out after 8000 m


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
