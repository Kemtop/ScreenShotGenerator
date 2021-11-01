using ScreenShotGenerator.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ScreenShotGenerator.Services.ScreenShoterPools
{
    /// <summary>
    /// Кеш сервиса.
    /// </summary>
    public class cacheRam
    {
 
        /// <summary>
        /// Кеш.
        /// </summary>
        private List<mCacheRam> cache = new List<mCacheRam>();
        
        /// <summary>
        /// Добавить задачу в кеш.
        /// </summary>
        /// <param name="job"></param>
        public void add(mJobPool job)
        {
            mCacheRam m = new mCacheRam();
            m.id = job.id;
            m.url = job.url;
            m.fileName = job.fileName;
            m.timestamp = job.timestamp;
            cache.Add(m);
        }

        /// <summary>
        /// Добавляет запись в кеш.
        /// </summary>
        /// <param name="cacheItem"></param>
        public void add(mCacheRam cacheItem)
        {
            cache.Add(cacheItem);
        }


            /// <summary>
            /// Количество элементов в кеши.
            /// </summary>
            /// <returns></returns>
            public int Count()
        {
            return cache.Count();
        }

        /// <summary>
        /// Ищет первый элемент с указанным урл.
        /// </summary>
        /// <param name="j"></param>
        /// <returns></returns>
        public mJobPool findUrl(string url)
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


        /// <summary>
        /// Удаляет записи, которые хранились более  hour часов.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public int clearOld(int hour)
        {
            int cnt = cache.Where(x => x.timestamp.AddHours(hour) < DateTime.Now).Count();
            cache.RemoveAll(x => x.timestamp.AddHours(hour) < DateTime.Now);
            return cnt;
        }

        /// <summary>
        /// Возвращает последние cnt запесей.
        /// </summary>
        /// <param name="cnt"></param>
        /// <returns></returns>
         public List<mCacheRam> getLastItems(int cnt)
        {
            return cache.OrderByDescending(x => x.id).Take(cnt).ToList();
        }
    }
}

