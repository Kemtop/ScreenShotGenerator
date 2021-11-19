using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Models
{
    public class SystemSettingModel
    {
        /// <summary>
        /// Период очистки кеша сервиса, в часах
        /// </summary>
        [Required]
        [Range(1, 99999)]
        public int clearCacheInterval { get; set; }

        /// <summary>
        /// Количество работающих браузеров.
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int browserMin { get; set; }

        /// <summary>
        /// Максимальное количество работающих браузеров.
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int browserMax { get; set; }

        /// <summary>
        ///Среднее время выполнения запроса (по практике).
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int averageTimeRequest { get; set; }
        
        /// <summary>
        /// Количество задач обрабатываемых одним браузером.
        /// </summary>
        [Required]
        [Range(1, 100000)]
        public int tasksAmount { get; set; }

        /// <summary>
        /// Информационное сообщение.
        /// </summary>
        public string InfoMessage { get; set; }

        /// <summary>
        /// Сообщение об ошибке ввода данных.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Количество элементов в кеши сервиса.
        /// </summary>
        public int cacheElementsCnt { get; set; }

        /// <summary>
        /// Количество обрабатываемых элементов на данный момент.
        /// </summary>
        public int curentElementsInProcessCnt { get; set; }

        /// <summary>
        /// Общий размер файлов во временной папке.
        /// </summary>
        public int cacheFilesSize { get; set; }

        /// <summary>
        /// Количество работающих браузеров(на текущий момент);
        /// </summary>
        public int browserCount { get; set; }

        /// <summary>
        /// Среднее время выполнения запроса.
        /// </summary>
        public string avgTimeRequest { get; set; }
    }
}
