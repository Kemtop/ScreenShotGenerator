using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Entities
{
    public class mPerformanceInfo
    {
        public int id { get; set; }
        public DateTime date { get; set; }
        public float cpuLoad { get; set; }
        public int memoryUsage { get; set; }
   
        /// <summary>
        /// Задачи ожидающие выполнения в пуле.
        /// </summary>
        public int poolWaiterTask { get; set; }

    }
}
