using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileConverter
{
    internal interface IConverter
    {
        /// <summary>
        /// Преобразовывает файл из одного вида в другой. 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="outFileName"></param>
        /// <param name="printInfo"></param>
        /// <param name="parcer"></param>
        void ConvertFile(string fileName, string outFileName, Action<string> printInfo,
            Func<List<string>, string, bool> parcer);
    }
}
