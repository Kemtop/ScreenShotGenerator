using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.ScreenShoterLogic
{
    /// <summary>
    /// Модель для ответа пользователю.
    /// </summary>
    public class mUserJson
    {
 
        public string url { get; set; }
        //Статус выполнения задачи.  1-выполнена. 0-ошибка
        public int status { get; set; }

         /// <summary>
        /// url путь к файлу.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// url путь к файлу.
        /// </summary>
        public string log { get; set; }

        public mUserJson()
        {
            log = "";
        }


    }
}
