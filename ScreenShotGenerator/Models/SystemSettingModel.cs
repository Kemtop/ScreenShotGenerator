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
        public int clearCashInterval { get; set; }

        /// <summary>
        /// Количество работающих браузеров.
        /// </summary>
        [Required]
        public int browserAmount { get; set; }

        /// <summary>
        /// Количество задач обрабатываемых одним браузером.
        /// </summary>
        [Required]
        public int tasksAmount { get; set; }


    }
}
