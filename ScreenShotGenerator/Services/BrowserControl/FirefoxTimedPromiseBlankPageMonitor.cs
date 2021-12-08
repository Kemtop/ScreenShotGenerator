using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Анализатор повторение ошибки TimedPromise timed out after при переходе на about:blank.
    /// </summary>
    public class FirefoxTimedPromiseBlankPageMonitor
    {
        /// <summary>
        /// Была ли при создании последнего скрин шота ошибка TimedPromise timed out after при переходе на about:blank.
        /// </summary>
        private bool hasLastTimedPromiseInBlankPage;

        /// <summary>
        /// Возникла какая то ошибка при переходе на about:blank. Если повторяется ошибка
        /// TimedPromise возвращаю true;
        /// </summary>
        /// <param name="error"></param>
        public bool ErrorToGoBlankPage(string error)
        {
            //Ошибка уже повторялась, браузер похоже умер.
            if (hasLastTimedPromiseInBlankPage && error.Contains("TimedPromise timed out after"))
                return true;

            if (error.Contains("TimedPromise timed out after")) //Получили искомую ошибку.
            {
                hasLastTimedPromiseInBlankPage = true;
                return false;
            }

            //Другая ошибка, сбрасываем "счетчик".
            hasLastTimedPromiseInBlankPage = false;

            return false;
        }

        /// <summary>
        /// Сброс ошибки TimedPromise если успешно перешли на главную страницу.
        /// </summary>
        public void ResetErrorTimedPromise()
        {
            hasLastTimedPromiseInBlankPage = false;
        }
    }
}
