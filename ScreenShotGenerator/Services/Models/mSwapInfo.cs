using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.Models
{
    /// <summary>
    /// Информация о процессе использующем swap.
    /// </summary>
    public class mSwapInfo
    {
        /// <summary>
        /// Идентификатор процесса.
        /// </summary>
        public UInt32 pid;
        /// <summary>
        /// Занимаемое место в swap.
        /// </summary>
        public UInt32 swap;
        /// <summary>
        /// Имя процесса.
        /// </summary>
        public string name;
    }
}
