﻿using System;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    ///Делегат для события-окончен процесс закрытия браузера. 
    /// </summary>
    public delegate void browserCloseDg(int id);

    /// <summary>
    /// Интерфейс для управления браузерами через Selenium.
    /// </summary>
    public interface IBrowserControl
    {
         /// <summary>
        /// Делегат для сохранения сведений об ошибках браузера.
        /// </summary>
        saveBrowserError saveBrowserErrorDg { get; set; }

        /// <summary>
        /// Настраивает драйвер, и вызвает запуск браузера.
        /// </summary>
        bool runBrowser();

        /// <summary>
        /// Закрыть браузер.
        /// </summary>
        void quit();

        /// <summary>
        /// Создает скрин шот,возвращает 1-все хорошо,0-ошибка сервиса,-1-браузер умер.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        int takeScreenShot(string url, string filePath, string filename, ref float elipsedTime, ImageSize imgSize, ref UInt32 outSize);

        /// <summary>
        /// Пустая страница на которую заходит браузер.
        /// </summary>
        string blankPage { get; set; }
        /// <summary>
        /// Возвращает ошибку.
        /// </summary>
        string lastError { get;}

        /// <summary>
        /// Событие возникающее когда браузер закрыт.
        /// </summary>
        event browserCloseDg eventClosed;
    }
}
