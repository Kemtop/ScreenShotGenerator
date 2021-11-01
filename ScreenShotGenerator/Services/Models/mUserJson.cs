using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.ScreenShoterPools
{
    /// <summary>
    /// Модель для ответа пользователю. По требованию заказчика.
    /// </summary>
    public class mUserJson
    {
        public string url { get; set; }

        /// <summary>
        ///Статус выполнения задачи.  1-выполнена. 0-ошибка 
        /// </summary>
        public int status { get; set; }

         /// <summary>
        /// Путь к файлу на хосте.
        /// </summary>
        public string path { get; set; }

        /// <summary>
        /// Сообщение об ошибках.
        /// </summary>
        public string log { get; set; }

        public mUserJson()
        {
            log = "";
        }

    }
}
