using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileConverter
{
    /// <summary>
    /// Преобразовывает файл из вида выгрузки phpMyAdmin в текстовый файл.
    /// </summary>
    public class Converter : IConverter
    {
        public void ConvertFile(string fileName, string outFileName, Action<string> printInfo,
            Func<List<string>, string, bool> parcer)
        {
            printInfo("Read file " + fileName);

            //Преобразоываем файл в список.
            var lines = new List<string>();
            parcer(lines, fileName);
          
            printInfo($"Read {lines.Count().ToString()}.");

            printInfo("Write to file " + outFileName);

            //Добавить запись в файл.

            printInfo("End");
        }
    }
}
