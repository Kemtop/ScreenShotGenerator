using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Entities
{
    /// <summary>
    /// Настройки сервиса.
    /// </summary>
    public class mServiceSettings
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Value { get; set; }
        /// <summary>
        /// Время последнего изменения.
        /// </summary>
        public DateTime LastChange { get; set; }

    }
}
