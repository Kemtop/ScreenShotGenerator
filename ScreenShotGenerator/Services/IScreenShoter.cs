using ScreenShotGenerator.Models;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    public interface IScreenShoter
    {
        /// <summary>
        /// Запускает сервис создания скрин шотов.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        void runService(CancellationToken cancellationToken);
        Task stopService(CancellationToken cancellationToken);

        //Запуск процесса создания скринов.
        List<mUserJson> runJob(string[] url, string userIP);

        /// <summary>
        /// Возвращает настройки сервиса.
        /// </summary>
        /// <returns></returns>
        SystemSettingModel getSettings();

        /// <summary>
        /// Задает настройки сервиса.
        /// </summary>
        /// <returns></returns>
        void setSettings(SystemSettingModel m);

        /// <summary>
        /// Перезапускает службу.
        /// </summary>
        void restartService();


        /// <summary>
        /// Возвращает количество ожидающих задач в пуле задач.
        /// </summary>
        /// <returns></returns>
        int getWaitTasksCnt();

        /// <summary>
        /// Возвращает список задач у которых статус не новый.
        /// </summary>
        /// <param name="top"></param>
        /// <returns></returns>
        List<mJobPool> getPoolTasksInfo(int top);

        /// <summary>
        /// Возвращает lastCnt последних записей в кеши.
        /// </summary>
        /// <param name="lastCnt"></param>
        /// <returns></returns>
        List<mCacheRam> getCacheItems(int lastCnt);
    }
}
