
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.ScreenShoterLogic
{
    /// <summary>
    /// Пул задач.
    /// </summary>
    public class poolTasks
    {
        /// <summary>
        /// Полное имя хоста с указаением порта.
        /// </summary>
        public string hostName { get; set; }
        //Директория для хранения временных файлов скринов.
        public String tmpDir { get; set; }


        List<mJobPool> pool = new List<mJobPool>();

        //Добавить значение в пул.
        public void add(mJobPool job)
        {
            pool.Add(job);
        }


        /// <summary>
        /// Количество элементов в пуле.
        /// </summary>
        /// <returns></returns>
        public int cacheCnt()
        {
            return pool.Count();
        }

        /// <summary>
        /// Количество обрабатываемых элементов на данный момент.
        /// </summary>
        public int curentElementsInProcessCnt()
        {
            IEnumerable<mJobPool> ret = pool.Where(x => x.status == 1);
            return ret.ToList().Count;
        }


        /// <summary>
        /// Возвращает firstCnt элементов требующих обработки.
        /// </summary>
        /// <param name="firstCnt"></param>
        /// <returns></returns>
        public List<mJobPool> getNeedProcessing(int firstCnt)
        {
            IEnumerable<mJobPool> ret = pool.Where(x => x.status == 0).OrderBy(x=>x.id).Take(firstCnt);
                    
            return ret.ToList();
        }

   
        /// <summary>
        /// Количество ожидающих запросов.
        /// </summary>
        /// <returns></returns>
        public int waitTasksCnt()
        {
            IEnumerable<mJobPool> ret = pool.Where(x => x.status == 0);
            return ret.ToList().Count;
        }



        /// <summary>
        /// Ищет первый элемент с указанным урл.
        /// </summary>
        /// <param name="j"></param>
        /// <returns></returns>
        public mJobPool findUrl(string url)
        {
            List<mJobPool> ret = pool.Where(x=>x.url==url).ToList();
            if (ret.Count > 0)
                return ret[0]; //Первый в списке, так как в случае глюков может быть и два.
            else
                return null;
        }

        /// <summary>
        /// Удалить завершенные задачи и задачи с ошибками.
        /// Возвращает количество удаленных.
        /// </summary>
        public int clearComplate()
        {
            int cnt = pool.Where(x => (x.status == 3 || x.status == 2)).Count();
            pool.RemoveAll(x=>(x.status==3||x.status==2));

            return cnt;
        }

    }
}
