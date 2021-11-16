using System;

namespace ScreenShotGenerator.Services.BrowserControl
{
 
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
        /// Создает скрин шот, в случае ошибок возвращает строку.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        string takeScreenShot(string url, string filePath, string filename, ref float elipsedTime, ImageSize imgSize, ref UInt32 outSize);

        /// <summary>
        /// Пустая страница на которую заходит браузер.
        /// </summary>
        string blankPage { get; set; }

    }
}
