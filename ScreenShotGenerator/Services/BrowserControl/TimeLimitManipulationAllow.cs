using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Ограничитель для логики увеличения/уменьшения количества работающих браузеров,
    ///что бы логика не меняла количество браузеров за то время за которое они не могут открыться или закрыться.
    /// </summary>
    public class TimeLimitManipulationAllow
    {
        /// <summary>
        /// Блокировка создания новых браузеров и отключение старых.
        /// Нужно что бы анализатор нагрузки не мешал своп монитору.
        /// </summary>
        private static bool lockBrowserManagment;

        /// <summary>
        /// Время последнего действия.
        /// </summary>
        private static DateTime lastSwitchBrowserAction=DateTime.Now;

        /// <summary>
        /// Можно ли манипулировать количеством браузеров? Увеличивать/уменьшать их количество.
        /// </summary>
        /// <returns></returns>
        public static bool WeCanManipulationBrowsersAmount()
        {
            if (lockBrowserManagment) return false; //Заблокирован внешней логикой.

            DateTime n = DateTime.Now;
            //Сколько прошло минут.
            double minute = (n - lastSwitchBrowserAction).TotalMinutes;
            if(minute>2.0) //Прошло больше x минут.
            {
                lastSwitchBrowserAction = n;
                return true;
            }
            return false;
        }


        /// <summary>
        ///  Блокировка создания новых браузеров и отключение старых системой регулировки нагрузки.
        /// </summary>
        public static void LockBrowserManagment()
        {
            lockBrowserManagment = true; //Запрет работы логики. 
        }


        /// <summary>
        ///  Разблокировка создания новых браузеров и отключение старых системой регулировки нагрузки.
        /// </summary>
        public static void UnlockBrowserManagment()
        {
            lastSwitchBrowserAction = DateTime.Now; //Обновление времени последних действий.
            lockBrowserManagment = false; //Запрет работы логики. 
        }


    }
}
