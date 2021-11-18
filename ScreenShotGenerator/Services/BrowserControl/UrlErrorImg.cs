using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Имена файлов используемые при отправки ошибок пользователю.
    /// </summary>    
    public class UrlErrorImg
    {
        /// <summary>
        /// Неверный URL адрес. Не существует или пуст.
        /// </summary>
        public static string badUrl = "page_not_found.jpg";

        /// <summary>
        /// Пустой объект screenshot. Какие то странности с сайтом.
        /// </summary>
        public static string badImg = "no_image_error.png";

        /// <summary>
        /// Является ли данный файл системной ошибкой.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static bool IsSystemErrorPage(string filename)
        {
            if (filename == badUrl) return true;
            if (filename == badImg) return true;

            return false;
        }
    }
}
