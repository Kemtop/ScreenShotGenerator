using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestServices
{
    class mTableTestResults
    {
        public int Id { get; set; }
        public string url { get; set; }
        /// <summary>
        /// json ответ сервера.
        /// </summary>
        public string response { get; set; }
        /// <summary>
        /// Время выполнения общего запроса.
        /// </summary>
        public string elapsedTime { get; set; }

        public DateTime create { get; set; }
    }
}
