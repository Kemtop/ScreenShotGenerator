using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Entities
{
    /// <summary>
    /// Модель таблицы Cash.
    /// </summary>
    public class mCashTable
    {
        public int Id { get; set; }
        /// <summary>
        /// Урл скрин которого делался.
        /// </summary>
        public string url { get; set; }

        /// <summary>
        /// Итоговый файл.
        /// </summary>
        public string fileName { get; set; }

        /// <summary>
        /// Размер файла в Кб.
        /// </summary>
        public int size { get; set; }


        /// <summary>
        /// Время затраченное на создание скрин шотта.
        /// </summary>
        public float wastedTime { get; set; }

        /// <summary>
        /// Временный отпечаток создания файла.
        /// </summary>
        public DateTime timestamp { get; set; }



    }
}
