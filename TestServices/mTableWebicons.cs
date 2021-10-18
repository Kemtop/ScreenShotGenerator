using System;
using System.Collections.Generic;
using System.Text;

namespace TestServices
{
    class mTableWebicons
    {
        public int id { get; set; }
        public string url { get; set; }
        //Статус 0, 1- в обработке,2 ошибка, 3 выполнено.
        public int status { get; set; }

    }
}
