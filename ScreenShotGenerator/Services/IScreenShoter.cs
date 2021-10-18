using ScreenShotGenerator.Services.ScreenShoterLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    public  interface IScreenShoter
    {
        /// <summary>
        /// Запускает сервис создания скрин шотов.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task runService(CancellationToken cancellationToken);
        Task stopService(CancellationToken cancellationToken);
        //Запуск процесса создания скринов.
        List<mUserJson> runJob(string[] url,string userIP);
       
    }
}
