using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Генерирует уникальный идентификатор браузера.
    /// </summary>
    public class BrowserIdGenerator
    {
        /// <summary>
        /// Уникальный идентификатор браузера.
        /// </summary>
        private static int browserId;

        /// <summary>
        /// Возвращает очередной идентификатор браузера.
        /// </summary>
        /// <returns></returns>
        public static int getId()
        {
            if (browserId < Int32.MaxValue - 10)
                browserId++;
            else
                browserId = 1;

            return browserId;
        }

    }
}
