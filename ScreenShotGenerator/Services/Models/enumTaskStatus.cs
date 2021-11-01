using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.Models
{
    /// <summary>
    /// Состояние задачи в пуле задач.
    /// </summary>
    public enum enumTaskStatus
    {
         NewTask=0,
         LockByBrowser=1,
         Error=2,
         End=3
    }
}
