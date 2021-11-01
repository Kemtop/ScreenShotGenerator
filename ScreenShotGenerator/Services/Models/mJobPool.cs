using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.ScreenShoterLogic
{
    /// <summary>
    /// Модель для работы с пулом задач для скрин шоттера.
    /// </summary>
    public class mJobPool
    {
        //Фактически номер в списке.
        public int id;
        public string url { get; set; }
        //Статус выполнения задачи. 0-ни кто о задаче не знает, 1-принята в обработку. 2-ошибка, 3-выполнена.
        public int status { get; set; }

        /// <summary>
        /// Итоговый файл.
        /// </summary>
        public string fileName;

        /// <summary>
        /// Временный отпечаток создания файла.
        /// </summary>
        public DateTime timestamp;

        /// <summary>
        /// Флаг нахождения объекта из кеши. Что бы повторно его не вставлять.
        /// </summary>
        public bool inCash;

        /// <summary>
        /// Идентификатор браузера, который взялся выполнять задачу.
        /// </summary>
        public int browserId;
    }
}
