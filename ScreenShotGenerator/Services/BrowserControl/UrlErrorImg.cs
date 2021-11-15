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
    }
}
