
using ScreenShotGenerator.Services.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.ScreenShoterPools
{
    /// <summary>
    /// Пул задач.
    /// </summary>
    public class PoolTasks
    {
        //Синхронизация потоков, для работы с общим пулом.
        static object lockPoolTask = new();

        List<mJobPool> pool = new List<mJobPool>();
        //BlockingCollection<mJobPool> pool = new BlockingCollection<mJobPool>();

        //Добавить значение в пул.
        public void add(mJobPool job)
        {
            lock (lockPoolTask)
            {
                pool.Add(job);
            }
        }
                        
        /// <summary>
        /// Количество обрабатываемых элементов на данный момент.
        /// </summary>
        public int curentElementsInProcessCnt()
        {
            lock (lockPoolTask)
            {
                IEnumerable<mJobPool> ret = pool.Where(x => x.status == 1);
                return ret.Count();
            }
        }

   
        /// <summary>
        /// Количество ожидающих запросов.
        /// </summary>
        /// <returns></returns>
        public int waitTasksCnt()
        {
            lock (lockPoolTask)
            {
                IEnumerable<mJobPool> ret = pool.Where(x => x.status == 0);
                return ret.Count();
            }
        }

        /// <summary>
        /// Удалить завершенные задачи и задачи с ошибками.
        /// Возвращает количество удаленных.
        /// </summary>
        public int clearComplate()
        {
            //Запрещает другим потокам работать с пулом на время его очистки.
            lock (lockPoolTask)
            {
                int cnt = pool.Where(x => (x.status == 3 || x.status == 2)).Count();
                pool.RemoveAll(x => (x.status == 3 || x.status == 2));

                return cnt;
            }
        }


        /// <summary>
        /// Возвращает firstCnt элементов из пула.
        /// </summary>
        /// <param name="firstCnt"></param>
        /// <returns></returns>
        public List<mJobPool> getItemInWork(int firstCnt)
        {
            lock (lockPoolTask)
            {
                // IEnumerable<mJobPool> ret = pool.Where(x => x.status > 0).OrderBy(x => x.id).Take(firstCnt);
                IEnumerable<mJobPool> ret = pool.OrderByDescending(x => x.id).Take(firstCnt);
                return ret.ToList();
            }
        }

        /// <summary>
        /// Выбирает из пула задач первые новые, в количестве tasksPerThread.
        /// Проставляет им статус "Заблокировано браузером".
        /// </summary>
        /// <param name="tasksPerThread"></param>
        /// <param name="browserId"></param>
        /// <returns></returns>
        public List<mJobPool> getAndLockNewTasks(int tasksPerThread,int browserId)
        {
            lock (lockPoolTask)
            {
                // Возвращает tasksPerThread элементов требующих обработки.
                List<mJobPool> data =pool.Where(x => x.status == 0).OrderBy(x => x.id).Take(tasksPerThread).ToList();

                //Есть новые задачи.
                if (data.Count > 0)
                {
                    //Блокирует для обработки. Другие потоки не будут обращать внимания на данные объекты.
                    foreach (mJobPool p in data)
                    {
                        p.status = (int)enumTaskStatus.LockByBrowser;
                        p.browserId = browserId;
                    }
                }

                return data;
            }

        }

    }
}
