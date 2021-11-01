using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.Models
{
    /// <summary>
    /// Модель объекта находящегося в кеши.
    /// </summary>
    public class mCacheRam
    {
        //Фактически номер в списке.
        public int id;
        public string url { get; set; }

        /// <summary>
        /// Итоговый файл.
        /// </summary>
        public string fileName;

        /// <summary>
        /// Временный отпечаток создания файла.
        /// </summary>
        public DateTime timestamp;

    }
}
