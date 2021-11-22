using ScreenShotGenerator.Models;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterPools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Интерфейс логики скриншоттера.
    /// </summary>
    public interface IScreenShoter
    {
        /// <summary>
        /// Запускает сервис создания скрин шотов.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        void runService(CancellationToken cancellationToken);

        /// <summary>
        /// Отстанавливает сервис.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task stopService(CancellationToken cancellationToken);


        /// <summary>
        /// Добавляет запросы на создания скрин шоттов в пулл и ждет завершения зачач.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="userIP"></param>
        /// <returns></returns>
        List<mUserJson> runJob(string[] url, string userIP, string conUUID);

        /// <summary>
        /// Возвращает настройки сервиса.
        /// </summary>
        /// <returns></returns>
        SystemSettingModel getSettings();

        /// <summary>
        /// Задает настройки сервиса и записывает их в БД.
        /// </summary>
        /// <returns></returns>
        void setSettings(SystemSettingModel m);

        /// <summary>
        /// Перезапускает сервис.
        /// </summary>
        void restartService();


        /// <summary>
        /// Возвращает количество ожидающих задач в пуле задач.
        /// </summary>
        /// <returns></returns>
        int getWaitTasksCnt();

        /// <summary>
        /// Возвращает top последних задач в пуле задач у которых статус не новый.
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

        /// <summary>
        /// Количество объектов в памяти.
        /// </summary>
        /// <returns></returns>
        int CacheItemsCount();

        /// <summary>
        /// Имена объектов на диске.
        /// </summary>
        /// <returns></returns>
        List<mImageList> DiskItems();

        /// <summary>
        /// Ищет файл на диске кодированный указанным url.
        /// </summary>
        /// <returns></returns>
        List<mImageList> FindFile(string url);

        /// <summary>
        /// Запуск процесса очистки.
        /// </summary>
        void RunCleaning(List<mImageList> diskItems);

        /// <summary>
        /// Возвращает размер файлов во временной папке в Мб.
        /// </summary>
        /// <returns></returns>
        int getTmpDirSize();
        /// <summary>
        /// Очищает все ошибки брайзера в БД.
        /// </summary>
        void ClearBrowserErrors();
    }
}
