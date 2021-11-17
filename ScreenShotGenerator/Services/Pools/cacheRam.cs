using ScreenShotGenerator.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ScreenShotGenerator.Services.ScreenShoterPools
{
    /// <summary>
    /// Кеш сервиса.
    /// </summary>
    public class CacheRam
    {
        /// <summary>
        /// Кеш.
        /// </summary>
        private List<mCacheRam> cache = new List<mCacheRam>();

        //Блокировка кеши poolCache.
        static object lockCachePool = new();

        /// <summary>
        /// Добавить задачу в кеш.
        /// </summary>
        /// <param name="job"></param>
        public void add(mJobPool job)
        {
            lock (lockCachePool)
            {
                mCacheRam m = new mCacheRam();
                m.id = job.id;
                m.url = job.url;
                m.fileName = job.fileName;
                m.timestamp = job.timestamp;
                m.wastedTime = job.wastedTime;
                m.fileSize = job.fileSize;
                cache.Add(m);
            }
        }

        /// <summary>
        /// Добавляет запись в кеш.
        /// </summary>
        /// <param name="cacheItem"></param>
        public void add(mCacheRam cacheItem)
        {
            lock (lockCachePool)
            {
                cache.Add(cacheItem);
            }
        }


        /// <summary>
        /// Количество элементов в кеши.
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            lock (lockCachePool)
            {
                return cache.Count();
            }
        }

        /// <summary>
        /// Ищет первый элемент с указанным урл.
        /// </summary>
        /// <param name="j"></param>
        /// <returns></returns>
        public mJobPool findUrl(string url)
        {
            lock (lockCachePool)
            {
                mCacheRam ret = cache.Where(x => x.url == url).FirstOrDefault();

                //Ни чего не найдено.
                if (ret == null) return null;

                mJobPool m = new mJobPool();
                m.id = ret.id;
                m.url = ret.url;
                m.status = 3;
                m.fileName = ret.fileName;
                m.timestamp = ret.timestamp;
                m.inCash = true;

                return m;
            }
        }


        /// <summary>
        /// Удаляет записи, которые хранились более  hour часов.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public int clearOld(int hour)
        {
            lock (lockCachePool)
            {
                int cnt = cache.Where(x => x.timestamp.AddHours(hour) < DateTime.Now).Count();
                cache.RemoveAll(x => x.timestamp.AddHours(hour) < DateTime.Now);
                return cnt;
            }
        }

        /// <summary>
        /// Возвращает последние cnt запесей.
        /// </summary>
        /// <param name="cnt"></param>
        /// <returns></returns>
        public List<mCacheRam> getLastItems(int cnt)
        {
            lock (lockCachePool)
            {
                return cache.OrderByDescending(x => x.id).Take(cnt).ToList();
            }
        }

        /// <summary>
        /// Возвращает начальные элементы размер которых не превышает указанный.
        /// Т.е. первые файлы, общий размер которых не более указанного значения.
        /// </summary>
        /// <returns></returns>
        public List<mCacheRam> getFirstElementsSomeSize(UInt64 size)
        {
            lock (lockCachePool)
            {
                UInt64 cnt = 0;
                IEnumerable<mCacheRam> tb = cache.OrderBy(x => x.id).
                       TakeWhile(x =>
                       {
                           cnt += x.fileSize;
                       //Выбрать начальные элементы сумма которых меньше заданного размера.
                       return size > cnt;
                       });

                return tb.ToList();
            }
        }

        /// <summary>
        /// Удаляет требуемые записи.
        /// </summary>
        /// <param name="range"></param>
        public void clearInterval(List<mCacheRam> items)
        {
            lock (lockCachePool)
            {
                cache.RemoveAll(x => items.Any(y => y.id == x.id));
            }
        }

    }
}

