using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Название ошибок браузера которые добавляются в таблицу.
    /// </summary>
    public enum enumBrowserError
    {
        PostProcessingCheckError=-1, //Ошибки проверки файла скрин шота.
        GoUrl=1,
        Debug=3, //Сообщения для долговременной отладки приложения. Браузер вернул пустой(null) заголовок.
        ProblemWithBrowser=2, //Критические ошибки с браузером.

    }
}
