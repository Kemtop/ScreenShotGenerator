using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Класс ожидания появления события для каждой задачи по данному url запросу.
    /// </summary>
    public class waiterEvent
    {
        /// <summary>
        /// Идентификатор номера запроса.
        /// </summary>
        public string requestId;
        /// <summary>
        /// Заставит ожидать прихода события.
        /// </summary>
        public AutoResetEvent signalizator = new AutoResetEvent(false);

    }
}
